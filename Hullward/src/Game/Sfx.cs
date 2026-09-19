using Godot;

namespace Hullward.Game;

/// <summary>
/// 音效与 BGM 管理器（Sprint 4 线 A · LD 规范 §4）。
/// 全部素材 CC0（Kenney），引用记录见 assets/引用记录表.md。
/// 每个事件独立 AudioStreamPlayer；射击双资源轮换避免叠爆音。
/// 默认 ProcessMode=Pausable：Esc 暂停时音频一并静止（LD 规范）。
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
    private bool _shotToggle;

    public override void _Ready()
    {
        // 显式 Pausable：Main 为 Always（暂停时收 Esc），Sfx 若不设会继承 Always 导致暂停时 BGM 不停
        ProcessMode = ProcessModeEnum.Pausable;

        _shot1 = Make("res://assets/audio/sfx/shot_1.ogg", 0.35f);
        _shot2 = Make("res://assets/audio/sfx/shot_2.ogg", 0.35f);
        _hit = Make("res://assets/audio/sfx/hit.ogg", 0.4f);
        _explosion = Make("res://assets/audio/sfx/explosion.ogg", 0.5f);
        _pickup = Make("res://assets/audio/sfx/pickup.ogg", 0.3f);
        _warp = Make("res://assets/audio/sfx/warp.mp3", 0.45f);   // 0.35→0.45：进任务瞬间注意力在画面上，需更突出
        _click = Make("res://assets/audio/sfx/click.ogg", 0.25f);
        _bossWarn = Make("res://assets/audio/sfx/boss_warn.mp3", 0.6f); // 0.45→0.6：用户反馈阶段警示不明显

        // BGM 循环（0.2 音量，用户反馈 0.3 偏大；播放完毕自动从头）
        var bgm = Make("res://assets/audio/bgm/theme.mp3", 0.2f);
        bgm.Finished += () => bgm.Play();
        bgm.Play();
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
}
