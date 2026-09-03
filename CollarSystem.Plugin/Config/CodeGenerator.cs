using System;

namespace CollarSystem.Plugin.Config;

/// Generates the short pairing codes exchanged out of band before the chat handshake (collar/pairing).
/// Alphabet excludes visually-ambiguous characters (0/O, 1/I/L) since these get read aloud or typed from
/// memory, not copy-pasted.
public static class CodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate(int length = 6)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        return new string(chars);
    }
}
