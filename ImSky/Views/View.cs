namespace ImSky.Views;

public abstract class View {
    public virtual void PreDraw() { }
    public abstract void Draw();
    public virtual void PostDraw() { }

    public virtual void OnActivate() { }
    public virtual void OnDeactivate() { }
}
