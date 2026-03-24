namespace FlourishWellness.Models
{
    public static class TimeHelper
    {
        private static readonly TimeZoneInfo _centralTime =
            TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

        /// <summary>
        /// Returns the current date/time in Central Time (CST/CDT).
        /// </summary>
        public static DateTime CstNow =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _centralTime);
    }
}
