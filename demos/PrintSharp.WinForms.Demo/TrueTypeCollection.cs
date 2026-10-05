using System.Buffers.Binary;
using PdfSharp.Fonts;

namespace PrintSharp.WinForms.Demo;

// PDFsharp 用に TTC コレクションの先頭フォントを独立した SFNT に変換する。
internal static class TrueTypeCollection
{
    public static byte[] ExtractFirstFont(byte[] collection)
    {
        if (BinaryPrimitives.ReadUInt32BigEndian(collection) != 0x74746366) return collection;
        int offset = checked((int)BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(12, 4)));
        int count = BinaryPrimitives.ReadUInt16BigEndian(collection.AsSpan(offset + 4, 2));
        int headerLength = 12 + count * 16;
        int size = headerLength;
        for (int i = 0; i < count; i++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(offset + 12 + i * 16 + 12, 4)));
            size = checked(size + (length + 3 & ~3));
        }
        var font = new byte[size];
        collection.AsSpan(offset, headerLength).CopyTo(font);
        int target = headerLength;
        int headOffset = -1;
        for (int i = 0; i < count; i++)
        {
            int record = 12 + i * 16;
            int source = checked((int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8, 4)));
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 12, 4)));
            collection.AsSpan(source, length).CopyTo(font.AsSpan(target));
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(record + 8, 4), (uint)target);
            if (BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record, 4)) == 0x68656164)
            {
                headOffset = target;
                font.AsSpan(target + 8, 4).Clear();
            }
            target += length + 3 & ~3;
        }
        uint checksum = 0;
        for (int i = 0; i < font.Length; i += 4)
            checksum = unchecked(checksum + BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(i, 4)));
        if (headOffset >= 0)
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headOffset + 8, 4), unchecked(0xB1B0AFBA - checksum));
        return font;
    }
}
