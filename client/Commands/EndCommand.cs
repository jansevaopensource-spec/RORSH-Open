// RORSH-Gate End Command

using System;
using System.Text.Json;
using System.Threading.Tasks;
using RorshGate.Core;

namespace RorshGate.Commands
{
    public class EndCommand
    {
        private readonly WssClient _client;
        private readonly TaskCompletionSource<bool> _tcs;

        public EndCommand(WssClient client)
        {
            _client = client;
            _tcs = new TaskCompletionSource<bool>();

            _client.OnMessageReceived += (s, msg) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(msg);
                    var root = doc.RootElement;

                    if (root.GetProperty("command").GetString() == "get-end")
                    {
                        string message = root.GetProperty("message").GetString() ?? "Goodbye!";
                        Console.WriteLine(message);
                        _tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in end command: {ex.Message}");
                    _tcs.TrySetResult(true);
                }
            };
        }

        public async Task<bool> ExecuteAsync()
        {
            await _client.SendCommandAsync("get-end");
            return await _tcs.Task;
        }
    }
}
