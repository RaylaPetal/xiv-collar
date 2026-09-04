using System;
using System.Runtime.InteropServices;

namespace Oathbound.Plugin.Commands;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct OathboundMoveController
{
    [FieldOffset(0x3F)] public byte MouseRunning;
    [FieldOffset(0x110)] public int WishdirChanged;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct OathboundFollowState
{
    [FieldOffset(0x4C4)] public short FollowingTarget;
}
