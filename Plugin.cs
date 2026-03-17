using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using FFXIVDiscordBridgePlugin.Chat;
using FFXIVDiscordBridgePlugin.Config;
using FFXIVDiscordBridgePlugin.Core;
using FFXIVDiscordBridgePlugin.Discord;
using FFXIVDiscordBridgePlugin.Discord.Interactions;
using FFXIVDiscordBridgePlugin.Gui;
using FFXIVDiscordBridgePlugin.Util;

namespace FFXIVDiscordBridgePlugin;

/// <summary>
/// Plugin entry point. Dalamud calls the constructor on load and Dispose on unload.
/// All subsystems are wired through DI — swap implementations here, not in the subsystems.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private readonly ServiceProvider _services;

    public Plugin(IDalamudPluginInterface pluginInterface, IPluginLog log, IClientState clientState,
                  IChatGui chatGui, IFramework framework, ICommandManager commandManager,
                  IDataManager dataManager)
    {
        var collection = new ServiceCollection();

        // ── Dalamud services ──────────────────────────────────────────────
        collection.AddSingleton(pluginInterface);
        collection.AddSingleton(log);
        collection.AddSingleton(clientState);
        collection.AddSingleton(chatGui);
        collection.AddSingleton(framework);
        collection.AddSingleton(commandManager);
        collection.AddSingleton(dataManager);

        // ── Config storage (swap this line to change the backend) ─────────
        collection.AddSingleton<IConfigStore, DalamudConfigStore>();

        // ── Utility services ──────────────────────────────────────────────
        collection.AddSingleton<ILocalizer, Localizer>();
        collection.AddSingleton<SpecialCharsHandler>();
        collection.AddSingleton<CharacterAvatarService>();

        // ── Core Discord services ─────────────────────────────────────────
        collection.AddSingleton<PermissionGuard>();
        collection.AddSingleton<WebhookSender>();
        collection.AddSingleton<BotService>();
        collection.AddSingleton<AdminRequestService>();

        // ── Game event sources — add new IGameEventSource types here ──────
        collection.AddSingleton<IGameEventSource, ChatEventSource>();
        collection.AddSingleton<IGameEventSource, DutyFinderEventSource>();
        collection.AddSingleton<IGameEventSource, RetainerSaleEventSource>();

        // ── Discord action handlers — add new IDiscordActionHandler types here
        collection.AddSingleton<IDiscordActionHandler, TellReplyActionHandler>();

        // ── GUI ───────────────────────────────────────────────────────────
        collection.AddSingleton<MainWindow>();
        collection.AddSingleton<AdminRequestWindow>();
        collection.AddSingleton<PluginGui>();

        _services = collection.BuildServiceProvider();

        Initialize();
    }

    private void Initialize()
    {
        var webhookSender = _services.GetRequiredService<WebhookSender>();
        var botService    = _services.GetRequiredService<BotService>();
        var chatGui       = _services.GetRequiredService<IChatGui>();

        // Wire all event sources → WebhookSender
        foreach (var source in _services.GetServices<IGameEventSource>())
        {
            source.OnDiscordMessage += webhookSender.EnqueueAsync;
            source.Initialize();
        }

        // Start the GUI (registers WindowSystem + /discordbridge command)
        _services.GetRequiredService<PluginGui>();

        // Start the bot (fire-and-forget; logs errors internally)
        _ = botService.StartAsync();

        // Print load notification to in-game chat
        var asm       = typeof(Plugin).Assembly;
        var version   = asm.GetName().Version is { } v ? $"v{v.Major}.{v.Minor}.{v.Build}" : "?";
        var buildTime = System.Attribute.GetCustomAttributes(asm, typeof(System.Reflection.AssemblyMetadataAttribute))
                              .Cast<System.Reflection.AssemblyMetadataAttribute>()
                              .FirstOrDefault(a => a.Key == "BuildTime")?.Value ?? "?";
        chatGui.Print($"[DiscordBridge] {version} geladen — Build: {buildTime}");
    }

    public void Dispose()
    {
        _services.GetRequiredService<BotService>().StopAsync().GetAwaiter().GetResult();

        foreach (var source in _services.GetServices<IGameEventSource>())
            source.Dispose();

        _services.GetRequiredService<PluginGui>().Dispose();
        _services.Dispose();
    }
}
