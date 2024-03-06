global using CurrencyTracker.Manager;
global using CurrencyTracker.Manager.Infos;
global using CurrencyTracker.Manager.Tools;
global using CurrencyTracker.Manager.Trackers;
global using CurrencyTracker.Manager.Trackers.Components;
global using CurrencyTracker.Manager.Trackers.Handlers;
global using CurrencyTracker.Manager.Transactions;
global using CurrencyTracker.Windows;
global using Dalamud;
global using ECommons;
global using static ECommons.GenericHelpers;
global using ECommons.Automation;
global using OmenTools.Helpers;
global using Dalamud.Game.Addon.Events;
global using Dalamud.Memory;
global using Dalamud.Configuration;
global using Dalamud.Game;
global using Dalamud.Interface.Utility.Raii;
global using Dalamud.Game.Addon.Lifecycle;
global using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
global using Dalamud.Game.ClientState.Conditions;
global using Dalamud.Game.ClientState.Objects;
global using Dalamud.Game.Command;
global using FFXIVClientStructs.FFXIV.Client.Game.Fate;
global using Dalamud.Game.ClientState.Objects.Types;
global using Dalamud.Game.Inventory.InventoryEventArgTypes;
global using Dalamud.Game.Text;
global using Dalamud.Game.Text.SeStringHandling;
global using Dalamud.Hooking;
global using Dalamud.Interface;
global using Dalamud.Interface.Colors;
global using Dalamud.Interface.Components;
global using Dalamud.Interface.Internal;
global using Dalamud.Interface.Utility;
global using Dalamud.Interface.Windowing;
global using Dalamud.IoC;
global using Dalamud.Plugin;
global using Dalamud.Plugin.Services;
global using Dalamud.Utility;
global using FFXIVClientStructs.FFXIV.Client.Game;
global using FFXIVClientStructs.FFXIV.Component.GUI;
global using ImGuiNET;
global using ImPlotNET;
global using Lumina.Excel;
global using Newtonsoft.Json;
global using Newtonsoft.Json.Linq;
global using System;
global using System.Collections.Concurrent;
global using System.Collections;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Globalization;
global using System.IO;
global using System.IO.Compression;
global using System.Linq;
global using System.Numerics;
global using System.Reflection;
global using System.Text.RegularExpressions;
global using System.Xml.Linq;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading.Tasks;
global using System.Timers;
global using Dalamud.Game.ClientState.Objects.Enums;
global using TinyPinyin;
global using IntervalUtility;
global using OmenTools.ImGuiOm;
global using OmenTools.Widgets;
global using static CurrencyTracker.Manager.Tools.Helpers;
global using static CurrencyTracker.Manager.Trackers.TerrioryHandler;
global using static CurrencyTracker.Plugin;
