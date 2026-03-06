#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Globalization;
using Avalonia.Data.Converters;

namespace ShareX.VideoEditor.Presentation.ViewModels;

/// <summary>
/// Static converter instances used by the VideoEditorWindow AXAML for panel visibility.
/// </summary>
public static class ToolPanelConverters
{
    public static readonly IValueConverter IsTrim = new PanelEqualityConverter(VideoEditorViewModel.ToolPanel.Trim);
    public static readonly IValueConverter IsCrop = new PanelEqualityConverter(VideoEditorViewModel.ToolPanel.Crop);
    public static readonly IValueConverter IsWatermark = new PanelEqualityConverter(VideoEditorViewModel.ToolPanel.Watermark);
    public static readonly IValueConverter IsExport = new PanelEqualityConverter(VideoEditorViewModel.ToolPanel.Export);

    public static readonly IValueConverter CropModeLabel = new BoolToStringConverter(
        trueValue: "Exit Crop Mode",
        falseValue: "Enter Crop Mode");

    private sealed class PanelEqualityConverter : IValueConverter
    {
        private readonly VideoEditorViewModel.ToolPanel _target;
        public PanelEqualityConverter(VideoEditorViewModel.ToolPanel target) => _target = target;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is VideoEditorViewModel.ToolPanel panel && panel == _target;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class BoolToStringConverter : IValueConverter
    {
        private readonly string _trueValue;
        private readonly string _falseValue;

        public BoolToStringConverter(string trueValue, string falseValue)
        {
            _trueValue = trueValue;
            _falseValue = falseValue;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? _trueValue : _falseValue;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
