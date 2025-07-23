using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AsarLib;

namespace OpenWeModPatch
{
    public class WeModPatcher
    {
        public enum Patches
        {
            EnablePro,
            DisableUpdates
        }

        public enum PatchState
        {
            Ok,
            Error,
            Exception,
            HasBackup
        }

        private static readonly Dictionary<Patches, AsarJsPatch> AllPatches = new Dictionary<Patches, AsarJsPatch>
        {
            {
                Patches.EnablePro, new AsarJsPatch("buildRequest", "vendor", "prototype.buildRequest=function(")
                {
                    Regex = "prototype\\.buildRequest=function\\([a-zA-Z0-9#]{1,5},[a-zA-Z0-9#]{1,5}\\){",
                    Patch = match => string.Format(
                        "{0}(function(){{if(this.interceptors?.[0]?.[\"{1:N}\"]!==!0){{let i=Object.assign({{\"{1:N}\":!0}},{2});this.interceptors?.length>0?this.interceptors.unshift(i):this.interceptors=[i]}}}}).apply(this,[]);",
                        match.Value, Guid.NewGuid(), @"{
    request: function (request) { return request; },
    requestError: function (error) { throw error; },
    response: async function (response, request) {
        if ([101, 204, 205, 304].indexOf(response.status) != -1 ||
            response.headers?.get('Content-Type') !== 'application/json') return response;
        const json = await response.json();
        if (json.hasOwnProperty('subscription')) {
            const year = (new Date()).getUTCFullYear();
            json.subscription = {
                startedAt: `${year}-01-01T00:00:00Z`,
                endsAt: `${year + 1}-01-01T00:00:00Z`,
                period: 'yearly',
                state: 'active',
                processor: 'recurly',
                nextInvoice: {
                    amount: 0,
                    currency: 'USD'
                }
            };
        }
        return new Response(JSON.stringify(json), {status: response.status, statusText: response.statusText, headers: response.headers});
    },
    responseError: function (error, request, httpClient) { throw error }
}")
                }
            },
            {
                Patches.DisableUpdates,
                new AsarJsPatch("isUpdaterAvailable", "index.js", "function isUpdaterAvailable(){")
                {
                    Regex = "function isUpdaterAvailable\\(\\)\\{",
                    Patch = match => string.Format("{0}return false;", match.Value)
                }
            }
        };

        private readonly Dictionary<Patches, AsarJsPatch> _enabledPatches = new Dictionary<Patches, AsarJsPatch>();

        public readonly string Executable;
        public readonly FileVersionInfo Version;

        private WeModPatcher(string executable)
        {
            Executable = executable;
            Version = FileVersionInfo.GetVersionInfo(executable);
        }

        private static List<PatchResult> CallPatch(string path, bool @override,
            Action<string, string, List<PatchResult>> patchFunc)
        {
            var backupPath = $"{path}.bak";
            var result = new List<PatchResult>();

            try
            {
                if (@override || !File.Exists(backupPath))
                {
                    if (File.Exists(path)) File.Copy(path, backupPath, true);
                    patchFunc(path, backupPath, result);
                    if (!result.Any(r => r.State == PatchState.Error || r.State == PatchState.Exception))
                        return result;
                }
                else
                {
                    result.Add(new PatchResult(PatchState.HasBackup, $"{Path.GetFileName(backupPath)} already exist"));
                    return result;
                }
            }
            catch (Exception exception)
            {
                result.Add(new PatchResult(PatchState.Exception, exception.Message));
            }

            try
            {
                File.Copy(backupPath, path, true);
                File.Delete(backupPath);
            }
            catch
            {
                // File is taken
            }

            return result;
        }

        public List<PatchResult> DisableAsarIntegrityValidation()
        {
            return CallPatch(Path.Combine(Path.GetDirectoryName(Executable)!, "version.dll"), true,
                (version, _, result) =>
                {
                    using (var stream =
                           typeof(WeModPatcher).Assembly.GetManifestResourceStream("OpenWeModPatch.asar.integrity"))
                    using (var outStream = File.Open(version, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream!.CopyTo(outStream, 16 * 1024);
                    }

                    result.Add(new PatchResult(PatchState.Ok, "Asar integrity validation disabled"));
                });
        }

        private void ApplyPatches(Dictionary<string, Filesystem.FileEntry> entries, List<PatchResult> result)
        {
            foreach (var patch in _enabledPatches)
            foreach (var entry in entries)
                if (entry.Value?.Files != null) ApplyPatches(entry.Value.Files, result);
                else if (patch.Value.TryPatch(entry))
                    result.Add(new PatchResult(PatchState.Ok, $"Patched: {entry.Key}::{patch.Value.Id}"));
        }

        public void EnablePatch(Patches patch, bool enabled)
        {
            if (enabled)
                _enabledPatches[patch] = AllPatches[patch];
            else _enabledPatches.Remove(patch);
        }

        public List<PatchResult> Patch(bool @override)
        {
            return CallPatch(Path.Combine(Path.GetDirectoryName(Executable)!, "resources", "app.asar"), @override,
                (path, backup, result) =>
                {
                    using (var stream = File.OpenRead(backup))
                    using (var fs = new Filesystem(stream))
                    {
                        var count = result.Count;
                        ApplyPatches(fs.Files, result);
                        if (result.Count == count)
                            result.Add(new PatchResult(PatchState.Error, "Patch failed"));
                        else using (var outStream = File.Create(path, 16 * 1024 * 1024, FileOptions.None))
                                fs.Save(outStream, out _);
                    }
                });
        }

        public static WeModPatcher? Create(string? executable = null)
        {
            if (executable == null)
            {
                var basePath = Environment.ExpandEnvironmentVariables("%LocalAppData%\\WeMod");
                if (!Directory.Exists(basePath)) return null;
                var stubPatch = Path.Combine(basePath, "WeMod.exe");
                if (!File.Exists(stubPatch)) return null;
                var version = FileVersionInfo.GetVersionInfo(stubPatch).FileVersion;
                var versionPath = Directory.GetDirectories(basePath, "app-*").FirstOrDefault(v =>
                {
                    var file = Path.Combine(v, "WeMod.exe");
                    return File.Exists(file) && FileVersionInfo.GetVersionInfo(file).FileVersion == version;
                });
                if (!Directory.Exists(versionPath)) return null;
                executable = Path.Combine(versionPath, "WeMod.exe");
            }

            return !File.Exists(executable) ? null : new WeModPatcher(executable);
        }

        public class PatchResult
        {
            public string Message;
            public PatchState State;

            public PatchResult(PatchState state, string message)
            {
                State = state;
                Message = message;
            }
        }
    }
}