using System.Reflection;
using System.Collections.Concurrent;
using System.Runtime.Loader;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using ProsperSunflower;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

var gameDir=Environment.GetEnvironmentVariable("VINTAGE_STORY")
 ?? throw new InvalidOperationException("Set VINTAGE_STORY to your Vintage Story 1.22.7 installation directory.");
AssemblyLoadContext.Default.Resolving+=(context,name)=>{
 foreach(var root in new[]{gameDir,Path.Combine(gameDir,"Lib"),Path.Combine(gameDir,"Mods")}){
  var p=Path.Combine(root,name.Name+".dll");if(File.Exists(p))return context.LoadFromAssemblyPath(p);
 }return null;
};
return Run();
static int Run(){
 int checks=0;
 void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 var index=new SunflowerIndex();var pos=new PlanterPosition(0,0,0);var owner=new object();bool planted=true;
 index.Add(pos,owner,()=>planted);
 int cells=0;
 for(int x=-12;x<=12;x++)for(int y=-12;y<=12;y++)for(int z=-12;z<=12;z++){
  bool expected=Math.Abs(x)<=10&&Math.Abs(y)<=10&&Math.Abs(z)<=10;
  var covered=index.Covered(x+.5,y+.5,z+.5);Check(covered==expected,"exact cube boundary");if(covered)cells++;
 }
 Check(cells==9261,"exactly 9261 block cells");
 Check(index.Apply(.2f,0,0,0,0)==1.5f,"recover in unstable area");
 Check(index.Apply(1.8f,0,0,0,0)==1.8f,"preserve higher mod stability");
 Check(index.Apply(.2f,0,11,0,0)==.2f,"outside unchanged");
 foreach(var storm in new[]{.001f,.53f,.7f,.99f})Check(index.Apply(.07f,storm,0,0,0)==.07f,"storms unchanged");
 Check(!index.Covered(double.NaN,0,0)&&!index.Covered(0,double.PositiveInfinity,0),"invalid query rejected");
 planted=false;Check(!index.Covered(0,0,0),"live removal stops immediately");planted=true;
 index.Add(new(1,1,1),new object(),()=>true);Check(index.Apply(.1f,0,0,0,0)==1.5f,"overlap no stacking");
 index.Remove(pos,new object());Check(index.Covered(-10,0,0),"wrong owner cannot unregister");
 var replacement=new object();index.Add(pos,replacement,()=>true);index.Remove(pos,owner);
 Check(index.Covered(-10,0,0),"late old unload cannot remove replacement");index.Remove(pos,replacement);
 Check(!index.Covered(-10,0,0),"unloaded planter removed");index.Clear();Check(!index.Covered(0,0,0),"world dispose clears registry");
 // Compare bucket queries with an independent full scan across negative coordinates
 // and chunk boundaries, including dynamic removal.
 var random=new Random(119);var positions=new List<PlanterPosition>();
 for(int n=0;n<80;n++){var p=new PlanterPosition(random.Next(-48,49),random.Next(-48,49),random.Next(-48,49));positions.Add(p);index.Add(p,p,()=>true);}
 for(int n=0;n<2500;n++){
  double x=random.Next(-55,56)+.01,y=random.Next(-55,56)+.99,z=random.Next(-55,56)+.5;
  Check(index.Covered(x,y,z)==positions.Any(p=>Math.Abs((int)Math.Floor(x)-p.X)<=10 && Math.Abs((int)Math.Floor(y)-p.Y)<=10 && Math.Abs((int)Math.Floor(z)-p.Z)<=10),"bucket lookup matches full scan");
 }
 index.Clear();
 // Check all six faces and corners across positive/negative bucket boundaries.
 foreach(int origin in new[]{-33,-17,-16,-1,0,15,16,31}){
  var at=new PlanterPosition(origin,origin,origin);index.Add(at,owner,()=>true);
  foreach(int dx in new[]{-11,-10,0,10,11})foreach(int dy in new[]{-11,-10,0,10,11})foreach(int dz in new[]{-11,-10,0,10,11})
   Check(index.Covered(origin+dx+.5,origin+dy+.5,origin+dz+.5)==(Math.Abs(dx)<=10&&Math.Abs(dy)<=10&&Math.Abs(dz)<=10),"radius faces and corners across buckets");
  Check(index.Covered(origin-10d,origin,origin),"negative edge inclusive");
  Check(!index.Covered(origin-10.00001,origin,origin),"before negative edge excluded");
  Check(index.Covered(origin+10.99999,origin,origin),"outermost positive block included");
  Check(!index.Covered(origin+11d,origin,origin),"next positive block excluded");
  index.Clear();
 }
 // Exercise the installed native planter and its GetProps deserialization.
 var seeds=new Item{Code=new AssetLocation("game:seeds-sunflower"),Attributes=JsonObject.FromJson("{\"untouched\":true}")};
 SunflowerSystem.AddPlantingSupport(seeds);
 Check(seeds.Attributes["untouched"].AsBool(),"preserve seed attributes");
 var pot=new BlockEntityPlantContainer{Block=new BlockPlantContainer{Code=new AssetLocation("game:clayplanter-blue-fired"),Attributes=JsonObject.FromJson("{\"plantContainerSize\":\"large\"}")}};
 var stack=new ItemStack(seeds);pot.Inventory[0].Itemstack=stack;
 Check(SunflowerSystem.Active(pot),"native planted seeds activate");
 var props=pot.GetProps(stack);Check(props!=null&&props.Shape.Base.ToString()=="game:block/plant/crop/sunflower/stage11","native rendering props resolve");
 Check(props!.Textures["normal7-12"].Base.ToString()=="game:block/plant/crop/sunflower/normal7-12","sunflower texture resolves");
 Check(props.Transform.ScaleXYZ.X==.6f,"sunflower scale resolves");
 pot.Inventory[0].Itemstack=null;Check(!SunflowerSystem.Active(pot),"empty sunflower-colored planter inactive");
 pot.Inventory[0].Itemstack=new ItemStack(new Item{Code=new AssetLocation("game:seeds-rye")});Check(!SunflowerSystem.Active(pot),"other seeds inactive");
 pot.Inventory[0].Itemstack=stack;pot.Block.Attributes=JsonObject.FromJson("{\"plantContainerSize\":\"small\"}");Check(!SunflowerSystem.Active(pot)&&pot.GetProps(stack)==null,"small pot excluded");
 // Invoke actual behavior lifecycle callbacks, as used by break/chunk unload.
 foreach(var callback in new[]{"OnBlockRemoved","OnBlockUnloaded","OnBlockBroken"}){
  var aura=new SunflowerAura(pot);var at=new PlanterPosition(-16,32,16);
  AccessTools.Field(typeof(SunflowerAura),"index").SetValue(aura,index);
  AccessTools.Field(typeof(SunflowerAura),"position").SetValue(aura,at);
  index.Add(at,aura,()=>true);Check(index.Covered(-16,32,16),"lifecycle setup indexed");
  if(callback=="OnBlockBroken")aura.OnBlockBroken();else AccessTools.Method(typeof(SunflowerAura),callback).Invoke(aura,null);
  Check(!index.Covered(-16,32,16),"native behavior callback unregisters: "+callback);
 }
 index.Add(new(-16,32,16),new object(),()=>true);Check(index.Covered(-16,32,16),"reloaded planter registers again");
 var custom=JObject.Parse("{\"shape\":{\"base\":\"other:sunflower\"}}");
 ((JObject)seeds.Attributes.Token!)["plantContainable"]!["largeContainer"]=custom;
 SunflowerSystem.AddPlantingSupport(seeds);Check(seeds.Attributes["plantContainable"]["largeContainer"]["shape"]["base"].AsString()=="other:sunflower","preserve existing mod artwork");
 // Exercise the real installed game's query under Harmony, including its storm data.
 var blocks=Stub.Make<IBlockAccessor>(m=>m.Name=="get_MapSizeY"?448:throw new NotSupportedException(m.Name));
 var world=Stub.Make<IWorldAccessor>(m=>m.Name=="get_BlockAccessor"?blocks:throw new NotSupportedException(m.Name));
 var api=Stub.Make<ICoreAPI>(m=>m.Name=="get_World"?world:throw new NotSupportedException(m.Name));
 var native=new SystemTemporalStability();
 AccessTools.Field(typeof(SystemTemporalStability),"api").SetValue(native,api);
 AccessTools.Field(typeof(SystemTemporalStability),"temporalStabilityEnabled").SetValue(native,true);
 AccessTools.Field(typeof(SystemTemporalStability),"stabilityNoise").SetValue(native,SimplexNoise.FromDefaultOctaves(4,.1,.9,123));
 var mod=new SunflowerSystem();mod.Index.Add(new(0,64,0),new object(),()=>true);
 var contexts=(ConcurrentDictionary<SystemTemporalStability,SunflowerSystem>)AccessTools.Field(typeof(SunflowerSystem),"contexts").GetValue(null)!;contexts[native]=mod;
 float baseline=native.GetTemporalStability(0d,64d,0d),outside=native.GetTemporalStability(20d,64d,0d);
 var h=new Harmony("prospersunflower.tests");
 try{
  h.Patch(SunflowerSystem.StabilityTarget(),postfix:new HarmonyMethod(typeof(SunflowerSystem),"StabilityAfter"));
  Check(native.GetTemporalStability(0d,64d,0d)==Math.Max(baseline,1.5f),"native patched query restores stability");
  Check(native.GetTemporalStability(20d,64d,0d)==outside,"native outside preserved");
  foreach(float strength in new[]{.55f,.72f,.95f}){
   native.StormData.nowStormActive=true;native.StormData.stormGlitchStrength=strength;
   float actual=native.GetTemporalStability(0d,64d,0d);
   h.UnpatchAll("prospersunflower.tests");float original=native.GetTemporalStability(0d,64d,0d);
   Check(actual==original,"native light/medium/heavy storms match unpatched query");
   h.Patch(SunflowerSystem.StabilityTarget(),postfix:new HarmonyMethod(typeof(SunflowerSystem),"StabilityAfter"));
  }
 }finally{h.UnpatchAll("prospersunflower.tests");contexts.TryRemove(native,out _);}
 Console.WriteLine($"PASS: {checks} checks: exact cube, bucket lookup, planting, lifecycle and native Harmony/storm behavior.");return 0;
}
public class Stub:DispatchProxy{
 public System.Func<MethodInfo,object?> Handler=null!;
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!);
 public static T Make<T>(System.Func<MethodInfo,object?> handler) where T:class{var proxy=Create<T,Stub>();((Stub)(object)proxy).Handler=handler;return proxy;}
}
