namespace helengine.media;
/// <summary>Validates ownership, bounded group recursion and explicit source handles before rendering.</summary>
public static class TransitionValidator {
    /// <summary>Collects scene-group and transition diagnostics without opening sources.</summary>
    public static IReadOnlyList<CompositionDiagnostic> Validate(CompositionDocument document,MediaCapabilities capabilities) {
        var errors=new List<CompositionDiagnostic>();var layers=document.Layers.Where(layer=>layer!=null && !string.IsNullOrEmpty(layer.Id)).GroupBy(layer=>layer.Id).ToDictionary(group=>group.Key,group=>group.First());var owned=new HashSet<string>(StringComparer.Ordinal);
        foreach(var layer in layers.Values) {
            if(layer.Members==null) {Add(errors,"invalid_group",layer.Id,"Group members cannot be null.");continue;}
            if(layer.Kind!="group" && layer.Members.Count>0) {Add(errors,"invalid_group",layer.Id,"Only a group may own children.");}
            foreach(string member in layer.Members) {if(member==null || !layers.ContainsKey(member)) {Add(errors,"missing_layer",layer.Id,"Group child is missing.");}else if(!owned.Add(member)) {Add(errors,"group_ownership",layer.Id,"A child requires one owning group.");}}
            if(layer.Kind=="group") {if(layer.Members.Count==0) {Add(errors,"invalid_group",layer.Id,"Groups require children.");}CheckTree(layer,layers,new HashSet<string>(),0,errors);}
            if(layer.Kind=="media" && document.Media.FirstOrDefault(media=>media?.Id==layer.MediaId) is MediaReference source && source.Kind=="video" && Valid(layer.Start) && Valid(layer.End) && Valid(layer.SourceIn) && Valid(layer.SourceOut) && Valid(source.Duration)) {
                if(layer.SourceIn<MediaTime.Zero || layer.SourceOut<=layer.SourceIn || layer.SourceOut>source.Duration || !layer.HoldLastFrame && layer.SourceIn+layer.End-layer.Start>layer.SourceOut) {Add(errors,"source_interval",layer.Id,"Video source does not cover its timeline interval.");}
            }
        }
        var valid=new List<CompositionTransition>();
        foreach(var transition in document.Transitions.Where(item=>item!=null)) {
            var descriptor=capabilities.Effects.FirstOrDefault(effect=>effect.Id==transition.EffectId && effect.Version==transition.EffectVersion);
            if(descriptor==null || descriptor.Category!="transition") {Add(errors,"invalid_transition",transition.Id,"Transition effect is not a registered transition.");}
            else if(transition.Parameters==null || transition.Parameters.Any(parameter=>!descriptor.Parameters.TryGetValue(parameter.Key,out var shape) || !shape.Accepts(parameter.Value))) {Add(errors,"invalid_transition",transition.Id,"Transition parameters must match the registered transition.");}
            if(!Valid(transition.Start) || !Valid(transition.Duration) || transition.Duration<=MediaTime.Zero) {continue;}
            var end=transition.Start+transition.Duration;
            foreach(string id in new[]{transition.FromLayer,transition.ToLayer}) {
                if(id==null || !layers.TryGetValue(id,out var layer)) {continue;}
                if(owned.Contains(id)) {Add(errors,"transition_group_member",transition.Id,"Transition endpoints must be complete root scenes.");}
                if(!Valid(layer.Start) || !Valid(layer.End) || layer.Start>transition.Start || layer.End<end) {Add(errors,"transition_source_interval",transition.Id,"Both scenes must cover the complete overlap.");}
            }
            foreach(var earlier in valid) {if(transition.Start<earlier.Start+earlier.Duration && earlier.Start<end && (new[]{transition.FromLayer,transition.ToLayer}).Intersect(new[]{earlier.FromLayer,earlier.ToLayer}).Any()) {Add(errors,"transition_conflict",transition.Id,"Overlapping transitions cannot share a scene.");}}
            valid.Add(transition);
        }
        foreach(var transition in valid) {CheckTransitions(transition.ToLayer,transition.FromLayer,valid,new HashSet<string>(),errors,transition.Id);}
        return errors;
    }
    /// <summary>Rejects cyclic or excessively nested group ownership and children outside their owner.</summary>
    static void CheckTree(VisualLayer layer,Dictionary<string,VisualLayer> layers,HashSet<string> stack,int depth,List<CompositionDiagnostic> errors) {
        if(depth>16 || !stack.Add(layer.Id)) {Add(errors,"group_cycle",layer.Id,"Group graph is cyclic or exceeds nesting limit.");return;}
        foreach(string id in layer.Members ?? []) {if(id!=null && layers.TryGetValue(id,out var child)) {if(Valid(layer.Start) && Valid(layer.End) && Valid(child.Start) && Valid(child.End) && (child.Start<layer.Start || child.End>layer.End)) {Add(errors,"group_interval",child.Id,"Child interval must be inside its owner.");}CheckTree(child,layers,stack,depth+1,errors);}}
        stack.Remove(layer.Id);
    }
    /// <summary>Rejects directed scene cycles without recursive unbounded traversal.</summary>
    static void CheckTransitions(string start,string goal,List<CompositionTransition> transitions,HashSet<string> visited,List<CompositionDiagnostic> errors,string id) {
        if(start==null || goal==null) {return;}var pending=new Stack<string>();pending.Push(start);
        while(pending.Count>0) {string current=pending.Pop();if(current==goal) {Add(errors,"transition_cycle",id,"Scene transition graph is cyclic.");return;}if(!visited.Add(current)) {continue;}foreach(var next in transitions.Where(item=>item.FromLayer==current && item.ToLayer!=null)) {pending.Push(next.ToLayer);}}
    }
    /// <summary>Tests rational validity without throwing for malformed JSON.</summary>
    static bool Valid(MediaTime time)=>time.Denominator>0;
    /// <summary>Appends a path-addressable validation diagnostic.</summary>
    static void Add(List<CompositionDiagnostic> errors,string code,string path,string message)=>errors.Add(new(){Code=code,Path=path,Message=message});
}
