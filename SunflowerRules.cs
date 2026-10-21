namespace ProsperSunflower;

public readonly record struct PlanterPosition(int X, int Y, int Z);

/// <summary>Loaded planters only. Queries inspect at most 27 neighboring buckets.</summary>
public sealed class SunflowerIndex
{
    readonly Dictionary<(int X,int Y,int Z), Dictionary<PlanterPosition, Entry>> buckets = new();
    sealed record Entry(object Owner, Func<bool> Active);
    public const int Radius = 10;
    static int Bucket(int n) => (int)Math.Floor(n / 16d);
    public static bool Covers(PlanterPosition p, double x, double y, double z) =>
        x >= p.X-(double)Radius && x < p.X+Radius+1d &&
        y >= p.Y-(double)Radius && y < p.Y+Radius+1d &&
        z >= p.Z-(double)Radius && z < p.Z+Radius+1d;
    public void Add(PlanterPosition p, object owner, Func<bool> active)
    {
        var key=(Bucket(p.X),Bucket(p.Y),Bucket(p.Z));
        if(!buckets.TryGetValue(key,out var entries)) buckets[key]=entries=new();
        entries[p]=new(owner,active);
    }
    public void Remove(PlanterPosition p, object owner)
    {
        var key=(Bucket(p.X),Bucket(p.Y),Bucket(p.Z));
        if(!buckets.TryGetValue(key,out var entries) || !entries.TryGetValue(p,out var e) || !ReferenceEquals(e.Owner,owner))return;
        entries.Remove(p);if(entries.Count==0)buckets.Remove(key);
    }
    public bool Covered(double x,double y,double z)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(z))return false;
        for(int bx=Bucket((int)Math.Floor(x)-Radius);bx<=Bucket((int)Math.Floor(x)+Radius);bx++)
        for(int by=Bucket((int)Math.Floor(y)-Radius);by<=Bucket((int)Math.Floor(y)+Radius);by++)
        for(int bz=Bucket((int)Math.Floor(z)-Radius);bz<=Bucket((int)Math.Floor(z)+Radius);bz++)
            if(buckets.TryGetValue((bx,by,bz),out var entries))
                foreach(var (p,e) in entries)
                    if(Covers(p,x,y,z)&&e.Active())return true;
        return false;
    }
    public float Apply(float original,float stormStrength,double x,double y,double z) =>
        stormStrength > 0 || original >= 1.5f || !Covered(x,y,z) ? original : 1.5f;
    public void Clear()=>buckets.Clear();
}
