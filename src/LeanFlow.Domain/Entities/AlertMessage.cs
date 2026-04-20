using System;

namespace LeanFlow.Domain.Entities
{
    public class AlertMessage
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Severity { get; set; } = "INFO";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public static class AlertSeverity
    {
        public const string Info = "INFO";
        public const string Warning = "WARNING";
        public const string Critical = "CRITICAL";
    }
}
