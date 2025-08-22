using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

namespace Timer.Models
{
    public class TimerRecord
    {
        [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        [Required]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "名称长度必须在1-100字符之间")]
        public string Name { get; set; } = "计时记录";

        [JsonPropertyName("isCountdown")] public bool IsCountdown { get; set; }

        [JsonPropertyName("startTime")] public DateTime StartTime { get; set; }

        [JsonPropertyName("endTime")] public DateTime EndTime { get; set; }

        [JsonPropertyName("duration")] public TimeSpan Duration { get; set; }

        [JsonPropertyName("countdownTime")] public TimeSpan? CountdownTime { get; set; }

        [JsonPropertyName("notes")]
        [StringLength(500, ErrorMessage = "备注长度不能超过500字符")]
        public string Notes { get; set; } = "";

        [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("tags")] public string[] Tags { get; set; } = [];

        [JsonPropertyName("category")]
        [StringLength(50, ErrorMessage = "分类长度不能超过50字符")]
        public string Category { get; init; } = "";

        // 计算属性
        [JsonIgnore] public string FormattedDuration => FormatTimeSpan(Duration);

        [JsonIgnore]
        public string FormattedCountdownTime => CountdownTime.HasValue ? FormatTimeSpan(CountdownTime.Value) : "";

        [JsonIgnore] public string TypeDisplay => IsCountdown ? "倒计时" : "计时器";

        [JsonIgnore] public bool IsLongDuration => Duration.TotalHours >= 1;

        [JsonIgnore] public bool IsToday => StartTime.Date == DateTime.Today;

        [JsonIgnore]
        public bool IsThisWeek
        {
            get
            {
                var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                return StartTime.Date >= startOfWeek;
            }
        }

        [JsonIgnore]
        public string RelativeTime
        {
            get
            {
                var timeSpan = DateTime.Now - StartTime;
                return timeSpan.TotalDays switch
                {
                    < 1 => "今天",
                    < 2 => "昨天",
                    < 7 => $"{(int)timeSpan.TotalDays}天前",
                    < 30 => $"{(int)(timeSpan.TotalDays / 7)}周前",
                    < 365 => $"{(int)(timeSpan.TotalDays / 30)}个月前",
                    _ => $"{(int)(timeSpan.TotalDays / 365)}年前"
                };
            }
        }

        // 验证方法
        public bool IsValid() =>
            !string.IsNullOrWhiteSpace(Name) &&
            StartTime != default &&
            EndTime != default &&
            EndTime >= StartTime &&
            Duration >= TimeSpan.Zero &&
            (!CountdownTime.HasValue || CountdownTime.Value >= TimeSpan.Zero);

        // 获取效率百分比（仅对倒计时有效）
        public double? GetEfficiencyPercentage()
        {
            if (!IsCountdown || !CountdownTime.HasValue || CountdownTime.Value == TimeSpan.Zero)
            {
                return null;
            }

            return Math.Min(100.0, Duration.TotalSeconds / CountdownTime.Value.TotalSeconds * 100);
        }

        // 格式化时间显示
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }

            if (timeSpan.TotalMinutes >= 1)
            {
                return $"{(int)timeSpan.TotalMinutes:D2}:{timeSpan.Seconds:D2}";
            }

            return $"{(int)timeSpan.TotalSeconds}秒";
        }

        // 克隆方法
        public TimerRecord Clone() =>
            new()
            {
                Id = Guid.NewGuid().ToString(), // 生成新的ID
                Name = Name + " (副本)",
                IsCountdown = IsCountdown,
                StartTime = StartTime,
                EndTime = EndTime,
                Duration = Duration,
                CountdownTime = CountdownTime,
                Notes = Notes,
                CreatedAt = DateTime.Now,
                Tags = (string[])Tags.Clone(),
                Category = Category
            };

        // 更新备注
        public void UpdateNotes(string notes)
        {
            Notes = notes.Trim() ?? "";
        }

        // 添加标签
        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            var tagList = Tags.ToList();
            var trimmedTag = tag.Trim();

            if (tagList.Contains(trimmedTag, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            tagList.Add(trimmedTag);
            Tags = tagList.ToArray();
        }

        // 移除标签
        public void RemoveTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            var tagList = Tags.ToList();
            tagList.RemoveAll(t => string.Equals(t, tag.Trim(), StringComparison.OrdinalIgnoreCase));
            Tags = tagList.ToArray();
        }

        // 重写ToString方法
        public override string ToString() => $"{Name} - {TypeDisplay} - {FormattedDuration}";

        // 重写Equals和GetHashCode
        public override bool Equals(object? obj) => obj is TimerRecord record && Id == record.Id;

        public override int GetHashCode() => Id.GetHashCode();
    }
}