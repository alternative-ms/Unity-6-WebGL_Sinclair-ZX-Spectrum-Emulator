// Based on ZX Spectrum 48K emulator by Dean Edis aka DeanTheCoder https://github.com/deanthecoder/ZXSpeculator
using System.Collections;
using System.Text;

namespace CSharp.Core.Extensions
{
    public static class EnumerableExtensions
    {
        public static string ToCsv(this IList collection)
        {
            var sb = new StringBuilder();
            foreach (var o in collection)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(o);
            }

            return sb.ToString();
        }
    }
}