using System;
using System.Linq;
using System.Text;
using Oathbound.Plugin.Safety;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.Shell;

namespace Oathbound.Plugin.Commands;

#pragma warning disable CS0649 // assigned via reflection by Svc.Hook.InitializeFromAttributes, not by the compiler

/// collar/restraints: the gag chat-mangling rule. Intercepts outgoing chat at the same point GagSpeak
/// hooks it - `ShellCommandModule.ProcessChatInput`, called after Enter is pressed but before the message
/// is handed off to the server - and rewrites the actually-transmitted text to a garbled variant, not just
/// the Sub's local display. This is a materially different automation surface from anything else in this
/// plugin: it rewrites content the Sub themselves typed rather than blocking an input or command (see
/// design.md's Risks/Trade-offs and the README's ToS-disclosure section). Same signature-hook risk/fail-
/// closed posture as MovementLockService: if ProcessChatInput's signature doesn't resolve on the current
/// game version, IsAvailable stays false and chat is never touched.
public sealed unsafe class ChatGagService : IRestrictionEnforcer, IDisposable
{
    private const string SigProcessChatInput = "E8 ?? ?? ?? ?? FE 87 ?? ?? ?? ?? C7 87";

    public unsafe delegate void ProcessChatInputDelegate(ShellCommandModule* uiModule, Utf8String* message, nint a3);

    [Signature(SigProcessChatInput, DetourName = nameof(ProcessChatInputDetour), Fallibility = Fallibility.Auto)]
    private readonly Hook<ProcessChatInputDelegate>? processChatInputHook;

    private bool active;

    public ChatGagService()
    {
        Svc.Hook.InitializeFromAttributes(this);

        IsAvailable = processChatInputHook is not null;
        if (IsAvailable)
            processChatInputHook!.Enable();
        else
            Plugin.Log.Error("ChatGagService: ProcessChatInput hook failed to resolve - gag chat-mangling is disabled for this session.");
    }

    public bool IsAvailable { get; }

    public void Engage()
    {
        if (IsAvailable)
            active = true;
    }

    public void Release() => active = false;

    private void ProcessChatInputDetour(ShellCommandModule* uiModule, Utf8String* message, nint a3)
    {
        if (!active)
        {
            processChatInputHook!.Original(uiModule, message, a3);
            return;
        }

        try
        {
            var original = message->ToString();
            if (!string.IsNullOrWhiteSpace(original) && !original.StartsWith('/'))
            {
                var garbled = Garble(original);
                if (garbled.Length > 0 && garbled.Length <= 500)
                    message->SetString(garbled);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "ChatGagService: failed to garble outgoing chat - sending original text.");
        }

        processChatInputHook!.Original(uiModule, message, a3);
    }

    /// A simple, self-contained muffled-speech transform: every alphabetic character becomes a syllable
    /// from a small gag-speak set, punctuation/spacing is preserved so the message still reads as a real
    /// muffled utterance rather than a wall of one repeated token.
    private static readonly string[] Syllables = ["mm", "mph", "hmm", "mmf", "mrph"];

    internal static string Garble(string text)
    {
        var sb = new StringBuilder();
        var syllableIndex = 0;
        var i = 0;
        while (i < text.Length)
        {
            if (char.IsLetter(text[i]))
            {
                var wordStart = i;
                while (i < text.Length && char.IsLetter(text[i]))
                    i++;
                var word = text[wordStart..i];
                var syllable = Syllables[syllableIndex++ % Syllables.Length];
                sb.Append(char.IsUpper(word[0]) ? char.ToUpperInvariant(syllable[0]) + syllable[1..] : syllable);
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        active = false;
        processChatInputHook?.Dispose();
    }
}
