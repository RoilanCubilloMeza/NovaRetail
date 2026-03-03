using Microsoft.Maui.Controls.Shapes;

namespace NovaRetail.Controls
{
   
    [ContentProperty(nameof(CardContent))]
    public class SectionCard : ContentView
    {
        private readonly VerticalStackLayout _stack;
        private Label? _titleLabel;


        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(SectionCard), string.Empty,
                propertyChanged: (b, _, __) => ((SectionCard)b).RefreshTitle());

        public static readonly BindableProperty CardContentProperty =
            BindableProperty.Create(nameof(CardContent), typeof(View), typeof(SectionCard), null,
                propertyChanged: (b, _, __) => ((SectionCard)b).RefreshCardContent());

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public View? CardContent
        {
            get => (View?)GetValue(CardContentProperty);
            set => SetValue(CardContentProperty, value);
        }


        public SectionCard()
        {
            _stack = new VerticalStackLayout { Spacing = 0 };

            base.Content = new Border
            {
                BackgroundColor = Colors.White,
                StrokeShape     = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusXl },
                Stroke          = new SolidColorBrush(UiConfig.BorderLight),
                StrokeThickness = UiConfig.StrokeThin,
                Padding         = new Thickness(20),
                Shadow          = UiConfig.CardShadow(),
                Content         = _stack
            };
        }


        private void RefreshTitle()
        {
            if (_titleLabel is not null)
                _stack.Remove(_titleLabel);

            _titleLabel = null;

            if (string.IsNullOrEmpty(Title)) return;

            _titleLabel = new Label
            {
                Text           = Title,
                FontSize       = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor      = UiConfig.TextPrimary,
                Margin         = new Thickness(0, 0, 0, 14)
            };
            _stack.Insert(0, _titleLabel);
        }

        private void RefreshCardContent()
        {
            foreach (var child in _stack.Children.Where(c => c != _titleLabel).ToList())
                _stack.Remove(child);

            if (CardContent is not null)
                _stack.Add(CardContent);
        }
    }
}

