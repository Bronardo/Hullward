namespace Hullward.Domain.Ships;

/// <summary>
/// 舰船型号（母稿 v1.0 四档平级船体）：轻巡 / 突击舰 / 战列舰 / 要塞舰。
/// 数值即母舰等级解锁阈值（Lv.N 解锁第 N 档，与章节解锁一致）。
/// </summary>
public enum ShipClass
{
    Scout = 1,
    Assault = 2,
    Battleship = 3,
    Fortress = 4
}

/// <summary>
/// 舰船目录：按型号创建船体 + 母舰等级解锁判定（ULO2 多态工厂入口）。
/// 船坞（母舰舰船选择）与读档/出战共用本目录，保证船型唯一来源。
/// </summary>
public static class ShipCatalog
{
    /// <summary>按型号创建新船体（默认轻巡）。</summary>
    public static ShipBase Create(ShipClass shipClass) => shipClass switch
    {
        ShipClass.Assault => new AssaultShip(),
        ShipClass.Battleship => new Battleship(),
        ShipClass.Fortress => new FortressShip(),
        _ => new ScoutShip()
    };

    /// <summary>型号是否已随母舰等级解锁（Lv.N 解锁第 N 档）。</summary>
    public static bool IsUnlocked(ShipClass shipClass, int mothershipLevel)
        => (int)shipClass <= mothershipLevel;

    /// <summary>所有型号（船坞按序展示）。</summary>
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

    /// <summary>型号定位描述（船坞展示）。</summary>
    public static string Role(ShipClass shipClass) => shipClass switch
    {
        ShipClass.Assault => "Strike: high firepower, fast",
        ShipClass.Battleship => "Heavy: raw power, line breaker",
        ShipClass.Fortress => "Bulwark: max survivability",
        _ => "Balanced: versatile starter"
    };
}
