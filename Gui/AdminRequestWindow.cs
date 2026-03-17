using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Discord;

namespace FFXIVDiscordBridgePlugin.Gui;

/// <summary>
/// Modal popup shown when a Discord user sends /requestadmin and no admin is configured yet.
/// Allows the in-game player to approve or deny the request.
/// </summary>
public sealed class AdminRequestWindow : Window
{
    private readonly AdminRequestService _requestService;
    private readonly IConfigStore        _configStore;
    private readonly BotService          _botService;
    private readonly IFramework          _framework;

    public AdminRequestWindow(AdminRequestService requestService, IConfigStore configStore,
                              BotService botService, IFramework framework)
        : base("Discord Bridge — Admin Request",
               ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        _requestService = requestService;
        _configStore    = configStore;
        _botService     = botService;
        _framework      = framework;

        IsOpen = false;

        // RequestReceived fires on a Discord thread — switch to the framework thread before touching UI state
        requestService.RequestReceived += () =>
            _framework.RunOnFrameworkThread(() => IsOpen = true);
    }

    public override void Draw()
    {
        var pending = _requestService.Pending;
        if (pending is null) { IsOpen = false; return; }

        ImGui.Text("A Discord user wants to become the bridge admin:");
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.28f, 0.55f, 1f, 1f),
                          $"  {pending.Username}");
        ImGui.SameLine();
        ImGui.TextDisabled($"(ID: {pending.UserId})");
        ImGui.Spacing();
        ImGui.Text("Grant admin access?");
        ImGui.Spacing();

        if (ImGui.Button("Yes", new Vector2(80, 0)))
            Approve(pending.UserId);

        ImGui.SameLine();

        if (ImGui.Button("No", new Vector2(80, 0)))
            Deny();
    }

    private void Approve(ulong userId)
    {
        var config = _configStore.Load();
        config.AdminDiscordUserId = userId;
        _configStore.Save(config);
        _requestService.Clear();
        IsOpen = false;

        _ = Task.Run(async () =>
        {
            await _botService.StopAsync();
            await _botService.StartAsync();
        });
    }

    private void Deny()
    {
        _requestService.Clear();
        IsOpen = false;
    }
}
