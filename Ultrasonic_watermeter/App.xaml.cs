using MaterialDesignThemes.Wpf;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media;

namespace Ultrasonic_watermeter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var theme = new Theme();
            theme.SetLightTheme();

            Color myPrimaryColor = (Color)ColorConverter.ConvertFromString("#3E59A1");
            Color mySecondaryColor = (Color)ColorConverter.ConvertFromString("Blue");

            theme.SetPrimaryColor(myPrimaryColor);
            theme.SetSecondaryColor(mySecondaryColor);

            var paletteHelper = new PaletteHelper();
            paletteHelper.SetTheme(theme);
        }
    }

}
