namespace CUIDAPP.Localization
{
    /// <summary>XAML: Text="{loc:T clave}" — se actualiza al cambiar el idioma.</summary>
    [ContentProperty(nameof(Clave))]
    public class TExtension : IMarkupExtension<BindingBase>
    {
        public string Clave { get; set; } = "";

        public BindingBase ProvideValue(IServiceProvider serviceProvider) => new Binding
        {
            Mode = BindingMode.OneWay,
            Path = nameof(TextoClave.Texto),
            Source = Localizador.Instancia.Para(Clave)
        };

        object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
    }
}
