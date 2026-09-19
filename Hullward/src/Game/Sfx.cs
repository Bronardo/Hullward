using Godot;

namespace Hullward.Game;

/// <summary>
/// sfx与 BGM 管理器（Sprint 4 线 A · LD norms §4）。
/// 全部素材 CC0（Kenney），引用record见 assets/引用record表.md。
/// 每个event独立 AudioStreamPlayer；射击双资源rotateavoidance叠爆音。
/// default ProcessMode=Pausable：Esc pause时音频一并静止（LD norms）。
/// </summary>
public partial class Sfx : Node
{
    private AudioStreamPlayer _shot1 = null!;
    private AudioStreamPlayer _shot2 = null!;
    private AudioStreamPlayer _hit = null!;
    private AudioStreamPlayer _explosion = null!;
    private AudioStreamPlayer _pickup = null!;
    private AudioStreamPlayer _warp = null!;
    private AudioStreamPlayer _click = null!;
    private AudioStreamPlayer _bossWarn = null!;
    private AudioStreamPlayer _bgmLounge = null!;
    private AudioStreamPlayer _bgmBattle = null!;
    private bool _shotToggle;

    public override void _Ready()
    {
        // 显式 Pausable：Main 为 Always（pause时收 Esc），Sfx 若不设会inheritance Always 导致pause时 BGM 不停
        ProcessMode = ProcessModeEnum.Pausable;

        _shot1 = Make("res://assets/audio/sfx/shot_1.ogg", 0.35f);
        _shot2 = Make("res://assets/audio/sfx/shot_2.ogg", 0.35f);
        _hit = Make("res://assets/audio/sfx/hit.ogg", 0.4f);
        _explosion = Make("res://assets/audio/sfx/explosion.ogg", 0.5f);
        _pickup = Make("res://assets/audio/sfx/pickup.ogg", 0.3f);
        _warp = Make("res://assets/audio/sfx/warp.mp3", 0.45f);   // 0.35→0.45：进mission瞬间noteforce在画面上，需更突出
        _click = Make("res://assets/audio/sfx/click.ogg", 0.25f);
        _bossWarn = Make("res://assets/audio/sfx/boss_warn.mp3", 0.6f); // 0.45→0.6：用户反馈phase警示不明显

        // 双 BGM：休闲（main menu/starmap/mothership）与combat（iteration 26 v0.6.26）
        // 0.2 音量；播放完毕auto从头
        _bgmLounge = Make("res://assets/audio/bgm/theme.mp3", 0.2f);
        _bgmLounge.Finished += () => _bgmLounge.Play();
        _bgmBattle = Make("res://assets/audio/bgm/battle.mp3", 0.2f);
        _bgmBattle.Finished += () => _bgmBattle.Play();
        _bgmLounge.Play();
    }

    private AudioStreamPlayer Make(string path, float volume)
    {
        var p = new AudioStreamPlayer
        {
            Stream = GD.Load<AudioStream>(path),
            VolumeDb = Mathf.LinearToDb(volume)
        };
        AddChild(p);
        return p;
    }

    public void PlayShot()
    {
        if (_shotToggle) _shot1.Play();
        else _shot2.Play();
        _shotToggle = !_shotToggle;
    }

    public void PlayHit() => _hit.Play();
    public void PlayExplosion() => _explosion.Play();
    public void PlayPickup() => _pickup.Play();
    public void PlayWarp() => _warp.Play();
    public void PlayClick() => _click.Play();
    public void PlayBossWarn() => _bossWarn.Play();

    /// <summary>切到combat BGM（The Arplands，upbeat 8-bit）。</summary>
    public void PlayBattleBgm()
    {
        _bgmLounge.Stop();
        if (!_bgmBattle.Playing) _bgmBattle.Play();
    }

    /// <summary>切回休闲 BGM（8bit Bossa）。</summary>
    public void PlayLoungeBgm()
    {
        _bgmBattle.Stop();
        if (!_bgmLounge.Playing) _bgmLounge.Play();
    }
}
