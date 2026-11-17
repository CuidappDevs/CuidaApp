namespace CUIDAPP.Services
{
    /// <summary>
    /// Prepara las fotos antes de subirlas: las convierte a JPG y las reduce. El servidor solo acepta
    /// JPG/PNG de hasta 10 MB, y los teléfonos actuales guardan fotos de más de 10 MB o en HEIC/WEBP.
    /// Respeta la orientación de la foto (EXIF). PDF y audio pasan sin cambios.
    /// </summary>
    public static class ImagenSubida
    {
        private const int LadoMaximo = 1920;   // suficiente para leer un documento y para la foto de perfil
        private const int CalidadJpg = 85;
        private static readonly string[] Imagenes = { ".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".bmp", ".gif" };

        public static bool EsImagen(string ruta) => Imagenes.Contains(Path.GetExtension(ruta).ToLowerInvariant());

        /// <summary>Ruta lista para subir: un JPG temporal reducido, o la original si no es imagen o no se pudo convertir.</summary>
        public static async Task<string> PrepararAsync(string ruta)
        {
            if (!EsImagen(ruta)) return ruta;
#if ANDROID
            try
            {
                return await Task.Run(() => ConvertirAndroid(ruta)) ?? ruta;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ImagenSubida] No se pudo preparar {Path.GetFileName(ruta)}: {ex.Message}");
                return ruta;
            }
#else
            await Task.CompletedTask;
            return ruta;
#endif
        }

#if ANDROID
        private static string? ConvertirAndroid(string ruta)
        {
            // 1) Medidas sin cargar la imagen, para decodificarla ya reducida (evita quedarse sin memoria).
            var medir = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            Android.Graphics.BitmapFactory.DecodeFile(ruta, medir);
            if (medir.OutWidth <= 0 || medir.OutHeight <= 0) return null;

            var muestra = 1;
            while (Math.Max(medir.OutWidth, medir.OutHeight) / (muestra * 2) >= LadoMaximo) muestra *= 2;

            using var original = Android.Graphics.BitmapFactory.DecodeFile(ruta, new Android.Graphics.BitmapFactory.Options { InSampleSize = muestra });
            if (original == null) return null;

            // 2) Escala final y giro según la orientación guardada por la cámara.
            var escala = Math.Min(1f, (float)LadoMaximo / Math.Max(original.Width, original.Height));
            var matriz = new Android.Graphics.Matrix();
            matriz.PostScale(escala, escala);
            var giro = GradosExif(ruta);
            if (giro != 0) matriz.PostRotate(giro);

            using var final = Android.Graphics.Bitmap.CreateBitmap(original, 0, 0, original.Width, original.Height, matriz, true);

            // 3) JPG en la caché de la app (se borra solo cuando Android necesita espacio).
            var destino = Path.Combine(FileSystem.CacheDirectory, $"subida_{Guid.NewGuid():N}.jpg");
            using (var salida = File.Create(destino))
                final.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, CalidadJpg, salida);
            return destino;
        }

        private static int GradosExif(string ruta)
        {
            try
            {
                var exif = new AndroidX.ExifInterface.Media.ExifInterface(ruta);
                return exif.GetAttributeInt(AndroidX.ExifInterface.Media.ExifInterface.TagOrientation, AndroidX.ExifInterface.Media.ExifInterface.OrientationNormal) switch
                {
                    AndroidX.ExifInterface.Media.ExifInterface.OrientationRotate90 => 90,
                    AndroidX.ExifInterface.Media.ExifInterface.OrientationRotate180 => 180,
                    AndroidX.ExifInterface.Media.ExifInterface.OrientationRotate270 => 270,
                    _ => 0
                };
            }
            catch { return 0; }
        }
#endif
    }
}
