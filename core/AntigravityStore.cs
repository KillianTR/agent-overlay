using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ClaudeSessionOverlay;

public static class AntigravityStore
{
    public static readonly string AntigravityDir = Path.Combine(PlatformPaths.Home, ".gemini", "antigravity");
    public static readonly string SummariesDb = Path.Combine(AntigravityDir, "conversation_summaries.db");
    public static readonly string PresenceDir = Path.Combine(AntigravityDir, "presence");
    public static readonly string BrainDir = Path.Combine(AntigravityDir, "brain");

    public static bool IsAvailable => File.Exists(SummariesDb);

    public static List<SessionInfo> LoadSessions()
    {
        var result = new List<SessionInfo>();
        if (!IsAvailable) return result;

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = SummariesDb,
                Mode = SqliteOpenMode.ReadOnly,
                DefaultTimeout = 1
            };

            using var conn = new SqliteConnection(builder.ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT conversation_id, title, workspace_uris, not_fully_idle, status, last_modified_time FROM conversation_summaries ORDER BY last_modified_time DESC LIMIT 15;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string convId = reader.IsDBNull(0) ? "" : reader.GetString(0);
                if (string.IsNullOrEmpty(convId)) continue;

                string title = reader.IsDBNull(1) ? "" : reader.GetString(1);
                string workspaceUris = reader.IsDBNull(2) ? "" : reader.GetString(2);
                int notFullyIdle = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));
                string status = reader.IsDBNull(4) ? "" : reader.GetString(4);
                string lastMod = reader.IsDBNull(5) ? "" : reader.GetString(5);

                string cwd = ParseWorkspacePath(workspaceUris);
                bool hasLock = File.Exists(Path.Combine(PresenceDir, $"{convId}.lock"));

                DateTimeOffset lastModTime = DateTimeOffset.UtcNow;
                if (DateTimeOffset.TryParse(lastMod, out var parsedTime))
                {
                    lastModTime = parsedTime;
                }

                double lastModMs = lastModTime.ToUnixTimeMilliseconds();
                double ageMinutes = (DateTimeOffset.UtcNow - lastModTime).TotalMinutes;

                bool isRunning = (notFullyIdle == 1 || status == "CASCADE_RUN_STATUS_RUNNING") && ageMinutes < 60;
                if (!isRunning && ageMinutes > 60) continue;

                LiveStatus liveStatus = LiveStatus.Idle;
                if (isRunning)
                {
                    liveStatus = CheckIfWaiting(convId) ? LiveStatus.Waiting : LiveStatus.Working;
                }

                var session = new SessionInfo
                {
                    SessionId = "agy_" + convId,
                    Title = string.IsNullOrWhiteSpace(title) ? "Antigravity Project" : title,
                    Cwd = cwd,
                    Provider = "Antigravity",
                    IsAlive = isRunning || ageMinutes < 30,
                    Status = liveStatus,
                    StatusSince = lastModMs,
                    TurnStartedAt = isRunning ? lastModMs : null,
                    LastCompletedAt = !isRunning ? lastModMs : null,
                    Measure = new MeasureDoc
                    {
                        Model = "Gemini",
                    }
                };

                result.Add(session);
            }
        }
        catch
        {
            // Ignore concurrent database read lock collisions
        }

        return result;
    }

    private static string ParseWorkspacePath(string workspaceUris)
    {
        if (string.IsNullOrWhiteSpace(workspaceUris)) return "";
        try
        {
            var uris = JsonSerializer.Deserialize<string[]>(workspaceUris);
            if (uris != null && uris.Length > 0)
            {
                string uri = uris[0];
                if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                {
                    string path = Uri.UnescapeDataString(uri[8..]);
                    return path.Replace('/', '\\');
                }
                return uri;
            }
        }
        catch { }
        return "";
    }

    private static bool CheckIfWaiting(string convId)
    {
        try
        {
            string logFile = Path.Combine(BrainDir, convId, ".system_generated", "logs", "transcript.jsonl");
            if (!File.Exists(logFile)) return false;

            using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0) return false;

            long offset = Math.Max(0, fs.Length - 4096);
            fs.Seek(offset, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);
            string tail = sr.ReadToEnd();

            var lines = tail.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0) return false;

            string lastLine = lines[^1];
            if (lastLine.Contains("\"ask_question\"") || lastLine.Contains("\"RequestFeedback\":true") || lastLine.Contains("ask_user"))
            {
                return true;
            }
        }
        catch { }
        return false;
    }
}
