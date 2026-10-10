using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Projecta.Execution;

namespace Projecta.Streaming;

internal static class BoundedLines
{
    internal sealed record Line(long Number, ReadOnlyMemory<byte> Bytes);
    internal static async IAsyncEnumerable<Line> Read(Stream stream, int maximum,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var line = new MemoryStream(Math.Min(maximum, 8192));
        long number = 1;
        while (true)
        {
            int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            int start = 0;
            for (int i = 0; i < count; i++)
            {
                if (buffer[i] != 10) continue;
                Append(buffer, start, i - start, line, maximum);
                yield return new(number++, line.ToArray());
                line.SetLength(0); start = i + 1;
                cancellationToken.ThrowIfCancellationRequested();
            }
            Append(buffer, start, count - start, line, maximum);
        }
        if (line.Length != 0) yield return new(number, line.ToArray());
    }
    private static void Append(byte[] buffer, int start, int length, MemoryStream line, int maximum)
    {
        if (line.Length + length > maximum) throw new ExecutionLimitException("jsonl-line-bytes");
        line.Write(buffer, start, length);
    }
}
