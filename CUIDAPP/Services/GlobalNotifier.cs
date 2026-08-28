namespace CUIDAPP.Services
{
    // Banner in-app (no es push del sistema operativo): aparece sobre la pantalla que
    // sea que esté viendo el usuario en ese momento, sin bloquear la interacción, y
    // desaparece solo. Se usa para avisar de mensajes de chat que llegan mientras el
    // usuario no tiene esa conversación abierta.
    public static class GlobalNotifier
    {
        public static void MostrarBanner(string titulo, string mensaje)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var currentPage = Shell.Current?.CurrentPage;
                    if (currentPage is not ContentPage cp || cp.Content is not Grid rootGrid)
                        return;

                    var banner = new Border
                    {
                        Stroke = Colors.Transparent,
                        BackgroundColor = Color.FromArgb("#111827"),
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                        Padding = new Thickness(16, 12),
                        Margin = new Thickness(14, 50, 14, 0),
                        VerticalOptions = LayoutOptions.Start,
                        Opacity = 0,
                        ZIndex = 999,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 2,
                            Children =
                            {
                                new Label { Text = titulo, FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Colors.White },
                                new Label { Text = mensaje, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#D1D5DB"), LineBreakMode = LineBreakMode.TailTruncation }
                            }
                        }
                    };

                    Grid.SetRowSpan(banner, 99);
                    Grid.SetColumnSpan(banner, 99);
                    rootGrid.Children.Add(banner);

                    await banner.FadeTo(1, 200);
                    await Task.Delay(3500);
                    await banner.FadeTo(0, 200);
                    rootGrid.Children.Remove(banner);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error mostrando banner: {ex.Message}");
                }
            });
        }
    }
}
