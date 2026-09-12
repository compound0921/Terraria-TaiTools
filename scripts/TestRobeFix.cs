using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.IO;

internal static class TestRobeFix
{
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
        {
            var name = new AssemblyName(e.Name).Name + ".dll";
            foreach (var directory in new[] { args[1], Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TerrariaPatcher") })
            {
                var path = Path.Combine(directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        var assembly = Assembly.LoadFrom(args[0]);
        var playerType = assembly.GetType("Terraria.Player", true);
        var itemType = assembly.GetType("Terraria.Item", true);
        var player = FormatterServices.GetUninitializedObject(playerType);
        var armor = Array.CreateInstance(itemType, 20);
        var inventory = Array.CreateInstance(itemType, 59);
        for (int i = 0; i < armor.Length; i++) armor.SetValue(NewItem(itemType, 1), i);
        for (int i = 0; i < inventory.Length; i++) inventory.SetValue(NewItem(itemType, 1), i);
        playerType.GetField("armor").SetValue(player, armor);
        playerType.GetField("inventory").SetValue(player, inventory);
        var pack = playerType.GetMethod("PackGemStaffFeatures");
        var robes = new[] { 1, 1282, 1285, 4256, 1283, 1284, 1286, 1287 };
        var shots = new[] { 121, 124, 597, 122, 123, 125, 126 };
        int cases = 0;
        foreach (var formal in robes)
        foreach (var social in robes)
        foreach (var held in new[] { 1, 6171 })
        for (int shot = 0; shot < shots.Length; shot++)
        {
            itemType.GetField("type").SetValue(armor.GetValue(1), formal);
            itemType.GetField("type").SetValue(armor.GetValue(11), social);
            itemType.GetField("type").SetValue(inventory.GetValue(0), held);
            var result = (float)pack.Invoke(player, new object[] { shots[shot] });
            var bits = BitConverter.ToInt32(BitConverter.GetBytes(result), 0);
            int expected = 1 << shot;
            for (int i = 1; i < robes.Length; i++)
                if (formal == robes[i] || social == robes[i]) expected |= 1 << (i - 1);
            if (held == 6171) expected |= 1 << 7;
            else if (formal == robes[shot + 1] || social == robes[shot + 1]) expected |= 1 << 8;
            if (bits != expected) throw new Exception("Mismatch formal=" + formal + " social=" + social + " shot=" + shots[shot] + " got=" + bits + " expected=" + expected);
            cases++;
        }
        Console.WriteLine("PASS: " + cases + " actual patched-method combinations; no game process or save loaded.");
        return 0;
    }
    static object NewItem(Type itemType, int id)
    {
        var item = FormatterServices.GetUninitializedObject(itemType);
        itemType.GetField("type").SetValue(item, id);
        itemType.GetField("stack").SetValue(item, 1);
        return item;
    }
}
