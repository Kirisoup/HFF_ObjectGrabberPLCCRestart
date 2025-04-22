using BepInEx;
using Bootstrap = BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using BepInEx.Logging;

namespace ObjectGrabberPLCCRestart;

[BepInPlugin(GUID, NAME, PluginInfo.PLUGIN_VERSION)]
[BepInProcess("Human.exe")]
[BepInDependency(OBJECT_GRABBER_GUID, ">= 1.3.1")]
[BepInDependency(PLCC_TIMER_GUID, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
	public const string NAME = nameof(ObjectGrabberPLCCRestart), GUID = $"tld.kirisoup.hff.{NAME}";
	
	const string OBJECT_GRABBER_GUID = "top.zman350x.hff.objectgrabber";
	const string PLCC_TIMER_GUID = "com.plcc.hff.timer";

	const string CMD_KEY = "grab_autoreset";

	static Plugin? _instance;
	public static Plugin Instance => _instance ?? throw new InvalidOperationException(
		$"Plugin \"{GUID}\" is not instanciated yet. ");
	
	static ManualLogSource? _logger;
	public static new ManualLogSource Logger => _logger ?? throw new InvalidOperationException(
		$"Plugin \"{GUID}\" is not instanciated yet. ");
	
	bool _timerRunning;
	ConfigEntry<bool> _enabled = null!;

	Plugin() {
		if (_instance is not null) {
			Destroy(this);
			throw new InvalidOperationException($"Plugin \"{GUID}\" is already instanciated. ");
		}
		_instance = this;
		_logger = base.Logger;
	}

	void Awake() 
	{
		if (!Bootstrap.Chainloader.PluginInfos.ContainsKey(PLCC_TIMER_GUID)) {
			Logger.LogWarning($"Plugin \"{PLCC_TIMER_GUID}\" is not loaded, {NAME} will do nothing. ");
			enabled = false;
			return;
		}

		_enabled = Config.Bind(
			section: "General", 
			key: "Enabled", 
			defaultValue: false, 
			description: "Whether would the grab count auto-reset when plcc-timer restarts. ");

		RegisterCommand();
	}

	void Start() {
		_timerRunning = Timer.Main.timer.timerRunning;
	}

	// implement with the state-machine approach since
	// plcc timer does not implement timer restart in a seperate method for us to hook onto
	void FixedUpdate() {
		if (!_enabled.Value) return;
		var prev = _timerRunning;
		_timerRunning = Timer.Main.timer.timerRunning;
		if (prev || !_timerRunning) return;
		ObjectGrabber.GrabberTracker.instance.ResetGrabCounter(print: false);
		Shell.Print("Grab counter auto-reset");
	}

	void OnDestroy() {
		UnregisterCommand();
	}

	private void RegisterCommand() 
	{
		const string CMD_DESC = "Configures auto-reset on/off. ";
		const string CMD_HELP = $"""
			Syntax: `{CMD_KEY} <option>`
			Available options:
				<u>h</u>elp
				<u>i</u>nspect
				<u>t</u>oggle
				<u>e</u>nable (on)
				<u>d</u>isable (off)
			""";

		static string? Interact(
			string? str, 
			ConfigEntry<bool> entry) 
		{
			var args = str?.ToLowerInvariant()
				.Split([' '], StringSplitOptions.RemoveEmptyEntries);
			if (args?.Length is not 1) return $"Expected 1 argument but {args?.Length ?? 0} is fed";
			(entry.Value, string? message) = args[0] switch {
				"h" or "help" => (entry.Value, CMD_HELP),
				"i" or "inspect" => (entry.Value, 
					$"auto-reset is currently {(entry.Value ? "enabled" : "disabled")}. "),
				"t" or "toggle" => (!entry.Value, 
					$"toggled auto-reset {(entry.Value ? "off" : "on")}. "),
				"d" or "off" or "disable" => (false, "disabled auto-reset. "),
				"e" or "on" or "enable" => (true, "enabled auto-reset. "),
				_ => (entry.Value, 
					$"Argument must be one of the following: help, query, toggle, disable(off), enable(on). ")
			};
			return message;
		}

		Shell.RegisterCommand(
			command: CMD_KEY, 
			onCommand: str => {
				if (Interact(str, _enabled) is string message) Shell.Print(message);
			},
			help: CMD_DESC);
	}

	private static void UnregisterCommand() {
		var shreg = AccessTools.Field(typeof(Shell), "commands")?.GetValue(null);
		var cmds = AccessTools.Field(typeof(CommandRegistry), "commandsStr")?.GetValue(shreg)
			as Dictionary<string, Action<string>>;
		cmds?.Remove(CMD_KEY);
	}
}