using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace HomeworkDock
{
    public static class Palette
    {
        public static readonly Color BackgroundTop = Color.FromRgb(0xFC, 0xF9, 0xF3);
        public static readonly Color BackgroundBottom = Color.FromRgb(0xF4, 0xEE, 0xE3);
        public static readonly Color Ink = Color.FromRgb(0x25, 0x22, 0x1D);
        public static readonly Color InkSoft = Color.FromRgb(0x4C, 0x46, 0x3D);
        public static readonly Color Muted = Color.FromRgb(0x8C, 0x84, 0x77);
        public static readonly Color Faint = Color.FromRgb(0xB3, 0xAA, 0x9C);
        public static readonly Color Rule = Color.FromRgb(0xEA, 0xE3, 0xD6);
        public static readonly Color RuleStrong = Color.FromRgb(0xDF, 0xD6, 0xC6);
        public static readonly Color Card = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static readonly Color CardHover = Color.FromRgb(0xFF, 0xFC, 0xF7);
        public static readonly Color Accent = Color.FromRgb(0xD9, 0x58, 0x3B);
        public static readonly Color AccentDeep = Color.FromRgb(0xBB, 0x41, 0x2A);
        public static readonly Color AccentSoft = Color.FromRgb(0xFA, 0xE8, 0xE1);
        public static readonly Color Green = Color.FromRgb(0x3C, 0x78, 0x62);
        public static readonly Color GreenSoft = Color.FromRgb(0xE8, 0xF1, 0xEC);
        public static readonly Color Amber = Color.FromRgb(0xA5, 0x6A, 0x17);
        public static readonly Color AmberSoft = Color.FromRgb(0xFA, 0xEF, 0xD9);
        public static readonly Color Low = Color.FromRgb(0x4D, 0x72, 0x66);
        public static readonly Color LowSoft = Color.FromRgb(0xE9, 0xF0, 0xEC);
        public static readonly Color Toast = Color.FromRgb(0x2A, 0x27, 0x22);
    }

    public static class UI
    {
        public static readonly FontFamily FontUI = new FontFamily("Microsoft YaHei UI, Segoe UI");
        public static readonly FontFamily FontNumeric = new FontFamily("Bahnschrift, Segoe UI");

        public static SolidColorBrush B(string hex)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public static SolidColorBrush B(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public static SolidColorBrush Alpha(Color color, byte alpha)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        public static TextBlock Text(string text, double size, Brush color, FontWeight weight)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontFamily = FontUI;
            block.FontSize = size;
            block.Foreground = color;
            block.FontWeight = weight;
            block.TextWrapping = TextWrapping.NoWrap;
            block.VerticalAlignment = VerticalAlignment.Center;
            TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
            return block;
        }

        public static TextBlock Text(string text, double size, Brush color)
        {
            return Text(text, size, color, FontWeights.Normal);
        }

        public static DropShadowEffect Shadow(double blur, double depth, double opacity)
        {
            DropShadowEffect effect = new DropShadowEffect();
            effect.Color = Color.FromRgb(0x3A, 0x2A, 0x18);
            effect.BlurRadius = blur;
            effect.ShadowDepth = depth;
            effect.Direction = 270;
            effect.Opacity = opacity;
            effect.RenderingBias = RenderingBias.Performance;
            return effect;
        }

        public static Border Rounded(Brush background, double radius)
        {
            Border border = new Border();
            border.Background = background;
            border.CornerRadius = new CornerRadius(radius);
            border.SnapsToDevicePixels = true;
            return border;
        }

        public static Path IconPath(string data, Brush stroke, double size, double thickness)
        {
            Path path = new Path();
            path.Data = Geometry.Parse(data);
            path.Stroke = stroke;
            path.StrokeThickness = thickness;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
            path.Fill = null;
            path.Width = size;
            path.Height = size;
            path.Stretch = Stretch.Uniform;
            path.SnapsToDevicePixels = false;
            return path;
        }

        public static Path FilledIconPath(string data, Brush fill, double size)
        {
            Path path = new Path();
            path.Data = Geometry.Parse(data);
            path.Fill = fill;
            path.Stroke = null;
            path.Width = size;
            path.Height = size;
            path.Stretch = Stretch.Uniform;
            return path;
        }

        public static Button FlatButton(object content, double radius, Action onClick)
        {
            Button button = new Button();
            button.Content = content;
            button.Padding = new Thickness(0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.BorderThickness = new Thickness(0);
            button.Background = Brushes.Transparent;
            button.Foreground = B(Palette.Ink);
            button.Cursor = Cursors.Hand;
            button.Focusable = false;
            button.FocusVisualStyle = null;
            button.SnapsToDevicePixels = true;
            button.Template = FlatButtonTemplate(radius);
            if (onClick != null)
            {
                button.Click += delegate { onClick(); };
            }
            ToolTipService.SetInitialShowDelay(button, 350);
            ToolTipService.SetShowDuration(button, 8000);
            return button;
        }

        public static Button Hover(Button button, Brush normal, Brush hover)
        {
            button.Background = normal;
            button.MouseEnter += delegate
            {
                if (button.IsEnabled) button.Background = hover;
            };
            button.MouseLeave += delegate
            {
                if (button.IsEnabled) button.Background = normal;
            };
            return button;
        }

        public static void SetName(FrameworkElement element, string name)
        {
            AutomationProperties.SetName(element, name);
        }

        public static void SetId(FrameworkElement element, string id)
        {
            AutomationProperties.SetAutomationId(element, id);
        }

        private static ControlTemplate FlatButtonTemplate(double radius)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Chrome";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
            border.SetValue(FrameworkElement.UseLayoutRoundingProperty, true);

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,
                new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,
                new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.SnapsToDevicePixelsProperty, true);
            border.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = border;
            return template;
        }

        public static void ClearAndSet(Panel panel, UIElement child)
        {
            panel.Children.Clear();
            if (child != null) panel.Children.Add(child);
        }

        public static Grid Row(params UIElement[] children)
        {
            Grid grid = new Grid();
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null) grid.Children.Add(children[i]);
            }
            return grid;
        }

        public static ColumnDefinition Col(double width)
        {
            return new ColumnDefinition { Width = new GridLength(width) };
        }

        public static ColumnDefinition Star()
        {
            return new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        }

        public static ColumnDefinition Auto()
        {
            return new ColumnDefinition { Width = GridLength.Auto };
        }

        public static RowDefinition AutoRow()
        {
            return new RowDefinition { Height = GridLength.Auto };
        }

        public static RowDefinition StarRow()
        {
            return new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };
        }

        public static Border Divider()
        {
            return new Border
            {
                Height = 1,
                Background = B(Palette.Rule),
                SnapsToDevicePixels = true,
                Margin = new Thickness(0)
            };
        }
    }
}
