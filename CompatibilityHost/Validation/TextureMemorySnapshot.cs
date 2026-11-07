using KillConfirmCompatibility.Controls;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace KillConfirmCompatibility.Validation;

// Logical 32-bit pixel footprint of reachable decoded textures, NOT a driver
// allocation measurement. The independent DXGI probe records real GPU usage.
internal static class TextureMemorySnapshot
{
    internal sealed class Texture
    {
        public string Reference { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public bool RenderTarget { get; set; }
        public long RgbaEstimateBytes { get; set; }
    }

    internal static List<Texture> Capture(IEnumerable<KillConfirmAnimation> layers)
    {
        var visited = new HashSet<object>(ReferenceComparer.Instance);
        var textures = new List<Texture>();
        foreach (FieldInfo field in typeof(KillConfirmAnimation).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            Walk(field.GetValue(null), field.Name, 0, visited, textures);
        int index = 0;
        foreach (var layer in layers) Walk(layer, "layer[" + index++ + "]", 0, visited, textures);
        return textures.OrderByDescending(texture => texture.RgbaEstimateBytes).ToList();
    }

    private static void Walk(object value, string path, int depth, HashSet<object> visited, List<Texture> textures)
    {
        if (value == null || value is string || depth > 12) return;
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || type.IsValueType || !visited.Add(value)) return;
        if (value is CanvasBitmap bitmap)
        {
            try
            {
                var size = bitmap.SizeInPixels;
                textures.Add(new Texture { Reference = path, Width = size.Width, Height = size.Height,
                    RenderTarget = bitmap is CanvasRenderTarget, RgbaEstimateBytes = (long)size.Width * size.Height * 4 });
            }
            catch (ObjectDisposedException) { }
            catch (System.Runtime.InteropServices.COMException) { } // disposed native bitmap
            return;
        }
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                Walk(entry.Value, path + "[" + entry.Key + "]", depth + 1, visited, textures);
            return;
        }
        if (value is IEnumerable enumerable)
        {
            if (type.IsArray && (type.GetElementType().IsPrimitive || type.GetElementType().IsValueType)) return;
            int index = 0;
            foreach (object item in enumerable) Walk(item, path + "[" + index++ + "]", depth + 1, visited, textures);
            return;
        }
        // Traverse only renderer-owned resource containers, never tasks, WinRT
        // devices, UI trees, delegates or the process-wide object graph.
        if (!(type.Namespace ?? "").StartsWith("KillConfirmCompatibility.Controls", StringComparison.Ordinal)) return;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            Walk(field.GetValue(value), path + "." + field.Name, depth + 1, visited, textures);
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new();
        public new bool Equals(object left, object right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}
