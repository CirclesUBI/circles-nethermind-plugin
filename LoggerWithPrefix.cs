// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Logging;

namespace Circles.Index;

public class LoggerWithPrefix : ILogger
{
    private readonly ILogger _logger;
    private readonly string _prefix;

    public LoggerWithPrefix(string prefix, ILogger logger)
    {
        _logger = logger;
        _prefix = prefix;
    }

    public void Info(string text)
    {
        _logger.Info($"{_prefix} {text}");
    }

    public void Warn(string text)
    {
        _logger.Warn($"{_prefix} {text}");
    }

    public void Debug(string text)
    {
        _logger.Debug($"{_prefix} {text}");
    }

    public void Trace(string text)
    {
        _logger.Trace($"{_prefix} {text}");
    }

    public void Error(string text, Exception ex)
    {
        _logger.Error($"{_prefix} {text}", ex);
    }

    public bool IsInfo { get; }
    public bool IsWarn { get; }
    public bool IsDebug { get; }
    public bool IsTrace { get; }
    public bool IsError { get; }
}
