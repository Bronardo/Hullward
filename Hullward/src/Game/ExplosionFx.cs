using Godot;

namespace Hullward.Game;

/// <summary>
/// 爆炸特效：CC0 素材 fire00-19 帧序列（Space Shooter Redux）播放一次后自毁。
/// 由 Main 在敌船/玩家被击毁处生成。
/// </summary>
public partial class ExplosionFx : Node2D
{
    public override void _Ready()
    {
        var anim = new AnimatedSprite2D
        {
            SpriteFrames = BuildFrames(),
            Animation = "boom",
            Frame = 0,
            Centered = true
        };
        anim.FrameChanged += () => { };
        AddChild(anim);
        anim.Play("boom");
        anim.AnimationFinished += () => QueueFree();
    }

    private static SpriteFrames BuildFrames()
    {
        var frames = new SpriteFrames();
        frames.AddAnimation("boom");
        frames.SetAnimationSpeed("boom", 28.0f);
        for (int i = 0; i < 20; i++)
        {
            var tex = GD.Load<Texture2D>($"res://assets/effects/fire{i:00}.png");
            frames.AddFrame("boom", tex);
        }
        return frames;
    }
}
