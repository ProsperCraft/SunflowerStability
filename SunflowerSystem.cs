using System.Reflection;
using System.Collections.Concurrent;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ProsperSunflower;

public sealed class SunflowerSystem : ModSystem
{
    const string BehaviorName="ProsperSunflowerAura", PatchId="prospercraft.sunflower";
    static readonly ConcurrentDictionary<SystemTemporalStability,SunflowerSystem> contexts=new();
    static Harmony? harmony;
    SystemTemporalStability? temporal;
    public SunflowerIndex Index {get;}=new();
    public override bool ShouldLoad(EnumAppSide side)=>true;
    public override double ExecuteOrder()=>1.1;
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockEntityBehaviorClass(BehaviorName,typeof(SunflowerAura));
        temporal=api.ModLoader.GetModSystem<SystemTemporalStability>();
        lock(contexts)
        {
            contexts[temporal]=this;
            if(harmony!=null)return;
            harmony=new Harmony(PatchId);
            harmony.Patch(StabilityTarget(),postfix:new HarmonyMethod(typeof(SunflowerSystem),nameof(StabilityAfter)));
            harmony.Patch(AccessTools.Method(typeof(BlockEntityPlantContainer),nameof(BlockEntityPlantContainer.GetBlockInfo)),
                postfix:new HarmonyMethod(typeof(SunflowerSystem),nameof(InfoAfter)));
        }
    }
    public static MethodInfo StabilityTarget()=>AccessTools.DeclaredMethod(typeof(SystemTemporalStability),nameof(SystemTemporalStability.GetTemporalStability),[typeof(double),typeof(double),typeof(double)])
        ?? throw new MissingMethodException("Native temporal stability query not found.");
    public override void AssetsFinalize(ICoreAPI api)
    {
        int count=0;
        foreach(var block in api.World.Blocks)
        {
            if(block is not BlockPlantContainer || block.Attributes?["plantContainerSize"].AsString()!="large")continue;
            if(!(block.BlockEntityBehaviors??[]).Any(b=>b.Name==BehaviorName))
                block.BlockEntityBehaviors=[..block.BlockEntityBehaviors??[],new BlockEntityBehaviorType{Name=BehaviorName,properties=JsonObject.FromJson("{}")}];
            count++;
        }
        var seeds=api.World.GetItem(new AssetLocation("game:seeds-sunflower"))
            ?? throw new InvalidOperationException("Sunflower seeds were not found.");
        AddPlantingSupport(seeds);
        api.Logger.Notification("[ProsperSunflower] Ready: {0} large planter types; 10-block radius on each axis, normal storms, no stacking.",count);
    }
    public static void AddPlantingSupport(Item seeds)
    {
        seeds.Attributes??=JsonObject.FromJson("{}");
        var attrs=(JObject)seeds.Attributes.Token!;
        if(attrs["plantContainable"] is not JObject) attrs["plantContainable"]=new JObject();
        var plant=(JObject)attrs["plantContainable"]!;
        // Preserve another mod's sunflower artwork when planting is already supported.
        plant["largeContainer"]??=JObject.Parse("""
        {"shape":{"base":"game:block/plant/crop/sunflower/stage11"},
         "textures":{"normal7-12":{"base":"game:block/plant/crop/sunflower/normal7-12"}},
         "transform":{"translation":{"x":0,"y":0.5,"z":0},"scale":0.6},"randomRotate":false}
        """);
    }
    public static bool Active(BlockEntityPlantContainer pot)=>
        pot.Block?.Attributes?["plantContainerSize"].AsString()=="large" &&
        pot.GetContents()?.Collectible?.Code?.ToString()=="game:seeds-sunflower";
    static void StabilityAfter(SystemTemporalStability __instance,double x,double y,double z,ref float __result)
    {
        if(contexts.TryGetValue(__instance,out var mod))
            __result=mod.Index.Apply(__result,__instance.StormStrength,x,y,z);
    }
    static void InfoAfter(BlockEntityPlantContainer __instance,StringBuilder dsc)
    {
        if(!Active(__instance))return;
        var mod=__instance.Api?.ModLoader.GetModSystem<SunflowerSystem>();
        bool storm=(mod?.temporal?.StormStrength??0)>0;
        dsc.AppendLine().Append(storm?"Temporal stabilization paused during storm.":"Temporal stabilization active.")
            .AppendLine().Append("Coverage: 10 blocks in every direction (21 x 21 x 21 blocks). Overlapping planters do not stack.");
    }
    public override void Dispose()
    {
        lock(contexts)
        {
            if(temporal!=null)contexts.TryRemove(temporal,out _);
            Index.Clear();
            if(contexts.IsEmpty){harmony?.UnpatchAll(PatchId);harmony=null;}
        }
        base.Dispose();
    }
}

public sealed class SunflowerAura(BlockEntity blockentity) : BlockEntityBehavior(blockentity)
{
    SunflowerIndex? index;
    PlanterPosition position;
    public override void Initialize(ICoreAPI api,JsonObject properties)
    {
        base.Initialize(api,properties);
        // Native temporal-stability coordinates describe the main world.
        if(Pos.dimension!=0 || Blockentity is not BlockEntityPlantContainer pot)return;
        position=new(Pos.X,Pos.Y,Pos.Z);
        index=api.ModLoader.GetModSystem<SunflowerSystem>().Index;
        index.Add(position,this,()=>SunflowerSystem.Active(pot));
    }
    public override void OnBlockRemoved(){index?.Remove(position,this);base.OnBlockRemoved();}
    public override void OnBlockUnloaded(){index?.Remove(position,this);base.OnBlockUnloaded();}
    public override void OnBlockBroken(IPlayer? byPlayer=null){index?.Remove(position,this);base.OnBlockBroken(byPlayer);}
}
