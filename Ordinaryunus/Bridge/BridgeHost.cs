// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ordinaryunus.Host;
using Ordinaryunus.Jobs;

namespace Ordinaryunus.Bridge;

/// <summary>
/// Parses → validates → auth-gates → dispatches every JS→C# message, and posts C#→JS responses/events
/// (MIMARI §3). The only piece that knows about WebView2 is the `postRaw` delegate the host passes in.
/// </summary>
public sealed class BridgeHost
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    readonly AppState _app;
    readonly Handlers _handlers;
    readonly Action<string> _postRaw;
    readonly object _protoGate = new();
    DateTime _lastProtocolError = DateTime.MinValue;

    public BridgeHost(AppState app, Action<string> postRaw)
    {
        _app = app;
        _postRaw = postRaw;
        _handlers = new Handlers(app, this);
        app.SnapshotChanged += () => { if (app.Unlocked) PostEvent("snapshotChanged", Mapper.BuildSnapshot(app)); };
        app.BrainChanged += () => { if (app.Unlocked) PostEvent("brainChanged", new { stamp = app.Stamp }); };
        JobStore.Changed += j => { if (app.Unlocked) PostEvent("jobUpdated", j); };
    }

    /// <summary>Handles one raw JS→C# message. Returns the response JSON to post back, or null when nothing
    /// should be sent (an envelope so broken we cannot even echo its id/type — only a rate-limited protocolError).</summary>
    public async Task<string?> HandleAsync(string raw)
    {
        if (raw.Length > 65_536) { ProtocolError("bad_request", "Mesaj çok uzun."); return null; }
        JsonDocument doc;
        try { doc = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 16 }); }
        catch (JsonException) { ProtocolError("bad_request", "Mesaj JSON değil."); return null; }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { ProtocolError("bad_request", "Mesaj nesne olmalı."); return null; }
            var keys = new HashSet<string>();
            foreach (var p in root.EnumerateObject()) keys.Add(p.Name);
            if (keys.Count != 3 || !keys.SetEquals(EnvelopeKeys)) { ProtocolError("bad_request", "Zarf alanları hatalı."); return null; }
            if (!root.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number || !idEl.TryGetInt64(out var id) || id < 1 || id > 2_147_483_647)
            { ProtocolError("bad_request", "id geçersiz."); return null; }
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
            { ProtocolError("bad_request", "type geçersiz."); return null; }
            string type = typeEl.GetString() ?? "";
            var payloadEl = root.TryGetProperty("payload", out var pEl) ? pEl : default;
            if (payloadEl.ValueKind == JsonValueKind.Undefined || payloadEl.ValueKind != JsonValueKind.Object)
            { ProtocolError("bad_request", "payload nesne olmalı."); return null; }

            if (!Payloads.RequestTypes.TryGetValue(type, out bool requiresAuth))
                return Err(id, type, "unknown_type", "Bilinmeyen istek türü.");

            object payload;
            try { payload = Payloads.Validate(type, payloadEl); }
            catch (BridgeException be) { return Err(id, type, be.Code, be.Message); }

            bool allowedLocked = Payloads.AllowedWhileLocked.Contains(type);
            if (requiresAuth && !_app.Unlocked && !allowedLocked) return Err(id, type, "locked", "Önce giriş yap.");

            try
            {
                var result = await _handlers.Dispatch(type, payload).ConfigureAwait(false);
                return Ok(id, type, result);
            }
            catch (BridgeException be) { return Err(id, type, be.Code, be.Message); }
            catch (Exception)
            {
                return Err(id, type, "failed", "Bir sorun oldu; tekrar dene.");
            }
        }
    }

    static readonly HashSet<string> EnvelopeKeys = ["id", "type", "payload"];

    public void PostEvent(string type, object payload)
    {
        var node = JsonSerializer.SerializeToNode(payload, payload.GetType(), JsonOptions);
        var obj = new JsonObject { ["id"] = 0, ["type"] = type, ["payload"] = node };
        _postRaw(obj.ToJsonString(JsonOptions));
    }

    static string Ok(long id, string type, object payload)
    {
        var node = JsonSerializer.SerializeToNode(payload, payload.GetType(), JsonOptions);
        var obj = new JsonObject { ["id"] = id, ["type"] = type, ["ok"] = true, ["payload"] = node };
        return obj.ToJsonString(JsonOptions);
    }

    static string Err(long id, string type, string code, string message)
    {
        var obj = new JsonObject
        {
            ["id"] = id, ["type"] = type, ["ok"] = false,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        };
        return obj.ToJsonString(JsonOptions);
    }

    void ProtocolError(string code, string message)
    {
        lock (_protoGate)
        {
            if ((DateTime.Now - _lastProtocolError).TotalSeconds < 1) return;
            _lastProtocolError = DateTime.Now;
        }
        PostEvent("protocolError", new { code, message });
    }
}
