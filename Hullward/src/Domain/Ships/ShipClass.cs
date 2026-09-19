namespace Hullward.Domain.Ships;

/// <summary>
/// ship型号（母稿 v1.0 四档平级hull）：轻巡 / assault ship / battleship / fortress。
/// stat即mothership levelunlockthreshold（Lv.N unlock第 N 档，与sectorunlock一致）。
/// </summary>
public enum ShipClass
{
    Scout = 1,
    Assault = 2,
    Battleship = 3,
    Fortress = 4
}

/// <summary>
/// ship目录：按型号创建hull + mothership levelunlock判定（ULO2 polymorphismfactory入口）。
/// dock（mothershipshipselect）与load/出战共用本目录，保证船型唯一来源。
/// </summary>
public static class ShipCatalog
{
    /// <summary>按型号创建新hull（default轻巡）。</summary>
    public static ShipBase Create(ShipClass shipClass) => shipClass switch
    {
        ShipClass.Assault => new AssaultShip(),
        ShipClass.Battleship => new Battleship(),
        ShipClass.Fortress => new FortressShip(),
        _ => new ScoutShip()
    };

    /// <summary>型号是否已随mothership levelunlock（Lv.N unlock第 N 档）。</summary>
    public static bool IsUnlocked(ShipClass shipClass, int mothershipLevel)
        => (int)shipClass <= mothershipLevel;

    /// <summary>所有型号（dock按序展示）。</summary>
    public static ShipClass[] All() =>
        new[] { ShipClass.Scout, ShipClass.Assault, ShipClass.Battleship, ShipClass.Fortress };

    /// <summary>型号名称。</summary>
    public static string DisplayName(ShipClass shipClass) => shipClass switch
    {
        ShipClass.Assault => "Assault",
        ShipClass.Battleship => "Battleship",
        ShipClass.Fortress => "Fortress",
        _ => "Scout"
    };

    /// <summary>型号locatedescription（dock展示）。</summary>
    public static string Role(ShipClass shipClass) => shipClass switch
    {
        ShipClass.Assault => "Strike: high firepower, fast",
        ShipClass.Battleship => "Heavy: raw power, line breaker",
        ShipClass.Fortress => "Bulwark: max survivability",
        _ => "Balanced: versatile starter"
    };
}
