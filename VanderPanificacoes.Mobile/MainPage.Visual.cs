using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace VanderPanificacoes.Mobile;

public sealed partial class MainPage
{
    private static string Dinheiro(decimal valor) => valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

    private static Label Texto(string texto, double tamanho = 14) => new()
    {
        Text = texto, FontSize = tamanho, TextColor = Color.FromArgb("#6C776F"), LineHeight = 1.2
    };

    private static Border Cartao(View conteudo) => new()
    {
        BackgroundColor = Colors.White, Stroke = Color.FromArgb("#E6E9E1"), StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 20 }, Padding = 20, Content = conteudo
    };

    private static View Campo(string titulo, View controle)
    {
        if (controle.Parent is Border anterior) anterior.Content = null;
        return new VerticalStackLayout
        {
        Spacing = 7,
        Children =
        {
            new Label { Text = titulo, FontSize = 10, CharacterSpacing = 1, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#66766B") },
            new Border { Stroke = Color.FromArgb("#DDE4DB"), StrokeShape = new RoundRectangle { CornerRadius = 12 }, Padding = new Thickness(12, 3), BackgroundColor = Color.FromArgb("#F8FAF6"), Content = controle }
        }
        };
    }

    private static Button BotaoDiscreto(string texto) => new()
    {
        Text = texto, BackgroundColor = Color.FromArgb("#E8EEE6"), TextColor = Color.FromArgb("#24583F"),
        FontSize = 13, Padding = new Thickness(14, 10), CornerRadius = 12
    };

    private static View CabecalhoMarca(string titulo, string subtitulo)
    {
        var marca = new HorizontalStackLayout { Spacing = 12, Children =
        {
            new Border { BackgroundColor = Color.FromArgb("#EBC780"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Padding = 12,
                Content = new Label { Text = "VP", TextColor = Color.FromArgb("#173D2E"), FontFamily = "OpenSansSemibold", FontSize = 20 } },
            new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 3, Children =
            {
                new Label { Text = "VANDER", FontSize = 15, CharacterSpacing = 2, FontFamily = "OpenSansSemibold", TextColor = Colors.White },
                new Label { Text = "PANIFICAÇÕES", FontSize = 9, CharacterSpacing = 2, TextColor = Color.FromArgb("#C8D9CC") }
            } }
        } };
        return new Border { BackgroundColor = Color.FromArgb("#173D2E"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 24 }, Padding = 24,
            Content = new VerticalStackLayout { Spacing = 16, Children = { marca,
                new Label { Text = titulo, FontSize = 28, FontFamily = "OpenSansSemibold", TextColor = Colors.White, LineBreakMode = LineBreakMode.WordWrap },
                new Label { Text = subtitulo, FontSize = 14, TextColor = Color.FromArgb("#C8D9CC") }
            } } };
    }

    private static View Etapa(string numero, string titulo) => new HorizontalStackLayout { Spacing = 10, Children =
    {
        new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 9 }, Padding = new Thickness(9, 5), BackgroundColor = Color.FromArgb("#F7EEDB"),
            Content = new Label { Text = numero, TextColor = Color.FromArgb("#8A652C"), FontFamily = "OpenSansSemibold", FontSize = 12 } },
        new Label { Text = titulo, FontFamily = "OpenSansSemibold", FontSize = 16, TextColor = Color.FromArgb("#173D2E"), VerticalOptions = LayoutOptions.Center }
    } };

    private static View Selo(string texto, bool atencao = false) => new Border
    {
        BackgroundColor = Color.FromArgb(atencao ? "#F8EDDA" : "#E5EEE4"), StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 10 }, Padding = new Thickness(12, 8),
        Content = new Label { Text = texto, FontSize = 12, TextColor = Color.FromArgb(atencao ? "#8A652C" : "#24583F") }
    };
}
