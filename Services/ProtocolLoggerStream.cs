using System.Text;
using Microsoft.Extensions.Logging;

namespace MailMessenger.Client.Services;

internal sealed class ProtocolLoggerStream : Stream
{
	private readonly ILogger logger;
	private readonly LogLevel level;
	private readonly StringBuilder line = new();
	private readonly object gate = new();

	public ProtocolLoggerStream(ILogger logger, LogLevel level = LogLevel.Debug)
	{
		this.logger = logger;
		this.level = level;
	}

	public override bool CanWrite => true;

	public override bool CanRead => false;

	public override bool CanSeek => false;

	public override long Length => 0;

	public override long Position
	{
		get => 0;
		set { }
	}

	public override void Flush()
	{
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		if (!logger.IsEnabled(level) || count <= 0)
		{
			return;
		}

		var text = Encoding.ASCII.GetString(buffer, offset, count);
		lock (gate)
		{
			foreach (var ch in text)
			{
				if (ch == '\n')
				{
					Emit();
				}
				else if (ch != '\r')
				{
					line.Append(ch);
				}
			}
		}
	}

	private void Emit()
	{
		if (line.Length == 0)
		{
			return;
		}

		logger.Log(level, "{ProtocolLine}", line.ToString());
		line.Clear();
	}

	public override int Read(byte[] buffer, int offset, int count)
		=> throw new NotSupportedException();

	public override long Seek(long offset, SeekOrigin origin)
		=> throw new NotSupportedException();

	public override void SetLength(long value)
		=> throw new NotSupportedException();

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			lock (gate)
			{
				Emit();
			}
		}

		base.Dispose(disposing);
	}
}
