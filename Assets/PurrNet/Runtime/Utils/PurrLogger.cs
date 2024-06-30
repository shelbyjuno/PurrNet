using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PurrNet.Logging
{
    public class PurrLogger
    {
        public static void Log(string message, Object reference = null, LogStyle logStyle = default, [CallerFilePath] string filePath = "")
        {
            LogMessage(message, reference, logStyle, LogType.Log, filePath);
        }

        public static void LogWarning(string message, Object reference = null, LogStyle logStyle = default, [CallerFilePath] string filePath = "")
        {
            LogMessage(message, reference, logStyle, LogType.Warning, filePath);
        }

        public static void LogError(string message, Object reference = null, LogStyle logStyle = default, [CallerFilePath] string filePath = "")
        {
            LogMessage(message, reference, logStyle, LogType.Error, filePath);
        }

        public static void LogException(string message, Object reference = null, LogStyle logStyle = default, [CallerFilePath] string filePath = "")
        {
            LogMessage(message, reference, logStyle, LogType.Exception, filePath);
        }

        private static void LogMessage(string message, Object reference, LogStyle logStyle, LogType logType, string filePath)
        {
            string formattedMessage = FormatMessage_Internal(message, logStyle, filePath);

            switch (logType)
            {
                case LogType.Log:
                    Debug.Log(formattedMessage, reference);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(formattedMessage, reference);
                    break;
                case LogType.Error:
                    Debug.LogError(formattedMessage, reference);
                    break;
                case LogType.Exception:
                    Debug.LogException(new Exception(formattedMessage), reference);
                    break;
            }
        }
        
        public static string FormatMessage(string message, LogStyle logStyle = default, [CallerFilePath] string filePath = "")
        {
            return FormatMessage_Internal(message, logStyle, filePath);
        }

        private static string FormatMessage_Internal(string message, LogStyle logStyle, string filePath)
        {
            string fileName = System.IO.Path.GetFileName(filePath).Replace(".cs", "");
            return $"<color=#{ColorUtility.ToHtmlStringRGB(logStyle.headerColor)}>[{fileName}]</color> <color=#{ColorUtility.ToHtmlStringRGB(logStyle.textColor)}>{message}</color>";
        }
    }

    public struct LogStyle
    {
        private Color? _headerColor, _textColor;
        public Color headerColor => _headerColor ?? Color.white;
        public Color textColor => _textColor ?? Color.white;
        
        public LogStyle(Color headerColor = default, Color textColor = default)
        {
            headerColor = headerColor == default ? Color.white : headerColor;
            this._headerColor = headerColor;
            
            textColor = textColor == default ? Color.white : textColor;
            this._textColor = textColor;
        }
    }
}
