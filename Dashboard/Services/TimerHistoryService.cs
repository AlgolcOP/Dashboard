using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dashboard.Models;

namespace Dashboard.Services
{
    /// <summary>
    /// TimerHistoryService 计时历史记录服务，负责保存、加载和删除历史记录
    /// </summary>
    public class TimerHistoryService : IDisposable
    {
        private readonly SemaphoreSlim _fileSemaphore = new(1, 1);
        private readonly string _historyFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public TimerHistoryService()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var appDataPath = Path.Combine(documentsPath, "Dashboard");

            // 确保目录存在
            try
            {
                Directory.CreateDirectory(appDataPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建目录失败: {ex.Message}");
                // 回退到临时目录
                appDataPath = Path.GetTempPath();
            }

            _historyFilePath = Path.Combine(appDataPath, "timer_history.json");

            // 配置JSON序列化选项
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public void Dispose()
        {
            _fileSemaphore?.Dispose();
        }

        /// <summary>
        /// 异步获取历史记录
        /// </summary>
        /// <returns>历史记录列表</returns>
        public async Task<List<TimerRecord>> GetHistoryAsync()
        {
            await _fileSemaphore.WaitAsync();
            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    return new List<TimerRecord>();
                }

                var json = await File.ReadAllTextAsync(_historyFilePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<TimerRecord>();
                }

                var records = JsonSerializer.Deserialize<List<TimerRecord>>(json, _jsonOptions);

                // 按时间倒序排列，最新的在前面
                return records?.OrderByDescending(r => r.CreatedAt).ToList() ?? new List<TimerRecord>();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON反序列化失败: {ex.Message}");
                // 备份损坏的文件
                await BackupCorruptedFileAsync();
                return new List<TimerRecord>();
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"文件读取失败: {ex.Message}");
                return new List<TimerRecord>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取历史记录失败: {ex.Message}");
                return new List<TimerRecord>();
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// 异步保存历史记录
        /// </summary>
        /// <param name="record">要保存的历史记录</param>
        /// <returns></returns>
        public async Task SaveRecordAsync(TimerRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            await _fileSemaphore.WaitAsync();
            try
            {
                var history = await GetHistoryInternalAsync();

                // 确保记录有唯一ID
                if (string.IsNullOrEmpty(record.Id))
                {
                    record.Id = Guid.NewGuid().ToString();
                }

                // 设置创建时间
                if (record.CreatedAt == default)
                {
                    record.CreatedAt = DateTime.Now;
                }

                // 检查是否已存在相同ID的记录
                var existingIndex = history.FindIndex(r => r.Id == record.Id);
                if (existingIndex >= 0)
                {
                    history[existingIndex] = record; // 更新现有记录
                }
                else
                {
                    history.Insert(0, record); // 新记录插入到开头
                }

                // 限制历史记录数量，避免文件过大
                const int maxRecords = 1000;
                if (history.Count > maxRecords)
                {
                    history.RemoveRange(maxRecords, history.Count - maxRecords);
                }

                await SaveHistoryInternalAsync(history);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存记录失败: {ex.Message}");
                throw; // 重新抛出异常，让调用者知道操作失败
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// 异步删除历史记录
        /// </summary>
        /// <param name="recordId">要删除的记录ID</param>
        /// <returns></returns>
        public async Task DeleteRecordAsync(string recordId)
        {
            if (string.IsNullOrWhiteSpace(recordId))
            {
                throw new ArgumentException("记录ID不能为空", nameof(recordId));
            }

            await _fileSemaphore.WaitAsync();
            try
            {
                var history = await GetHistoryInternalAsync();
                var removedCount = history.RemoveAll(r => r.Id == recordId);

                if (removedCount > 0)
                {
                    await SaveHistoryInternalAsync(history);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"删除记录失败: {ex.Message}");
                throw;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// 异步清空历史记录
        /// </summary>
        /// <returns></returns>
        public async Task ClearHistoryAsync()
        {
            await _fileSemaphore.WaitAsync();
            try
            {
                await SaveHistoryInternalAsync(new List<TimerRecord>());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清空历史记录失败: {ex.Message}");
                throw;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <summary>
        /// 异步获取记录数量
        /// </summary>
        /// <returns>历史记录数量</returns>
        public async Task<int> GetRecordCountAsync()
        {
            var history = await GetHistoryAsync();
            return history.Count;
        }

        /// <summary>
        /// 根据计时类型异步获取记录
        /// </summary>
        /// <param name="isCountdown">是否为倒计时记录</param>
        /// <returns>符合条件的历史记录列表</returns>
        public async Task<List<TimerRecord>> GetRecordsByTypeAsync(bool isCountdown)
        {
            var history = await GetHistoryAsync();
            return history.Where(r => r.IsCountdown == isCountdown).ToList();
        }

        private async Task<List<TimerRecord>> GetHistoryInternalAsync()
        {
            // 内部方法，不需要获取信号量
            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    return new List<TimerRecord>();
                }

                var json = await File.ReadAllTextAsync(_historyFilePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<TimerRecord>();
                }

                return JsonSerializer.Deserialize<List<TimerRecord>>(json, _jsonOptions) ?? new List<TimerRecord>();
            }
            catch
            {
                return new List<TimerRecord>();
            }
        }

        private async Task SaveHistoryInternalAsync(List<TimerRecord> history)
        {
            // 内部方法，不需要获取信号量
            try
            {
                // 创建临时文件，原子性写入
                var tempFilePath = _historyFilePath + ".tmp";
                var json = JsonSerializer.Serialize(history, _jsonOptions);

                await File.WriteAllTextAsync(tempFilePath, json);

                // 原子性替换文件
                if (File.Exists(_historyFilePath))
                {
                    File.Delete(_historyFilePath);
                }

                File.Move(tempFilePath, _historyFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存文件失败: {ex.Message}");

                // 清理临时文件
                var tempFilePath = _historyFilePath + ".tmp";
                if (!File.Exists(tempFilePath))
                {
                    throw;
                }

                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // 忽略清理失败
                }

                throw;
            }
        }

        private async Task BackupCorruptedFileAsync()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var backupPath = _historyFilePath + $".backup.{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(_historyFilePath, backupPath);
                    Debug.WriteLine($"已备份损坏的文件到: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"备份损坏文件失败: {ex.Message}");
            }

            await Task.CompletedTask;
        }
    }
}