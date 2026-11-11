using Mozdulj.Core;
using Mozdulj.Maui.Services;
using System.Text.Json;

namespace Mozdulj.Maui.Pages;

/// <summary>Full-screen modal editor, owned by a NavigationPage. Built-ins never enter this page.</summary>
public sealed class ExerciseEditorPage : ContentPage
{
    private readonly AppState _state;
    private readonly Exercise _draft;
    private readonly Entry _name = new() { Placeholder = "Gyakorlat neve" };
    private readonly Picker _category = new() { Title = "Mozgástípus" };
    private readonly Entry _regions = new() { Placeholder = "pl. váll, hát" };
    private readonly VerticalStackLayout _newRegionGroups = new() { Spacing = 6 };
    private readonly Dictionary<string, Picker> _regionGroupPickers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _regionGroupChoices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Picker _phase = new() { Title = "Edzésfázis" };
    private readonly Picker _difficulty = new() { Title = "Nehézség" };
    private readonly Picker _measure = new() { Title = "Mérés típusa" };
    private readonly Entry _seconds = new() { Placeholder = "Idő másodpercben", Keyboard = Keyboard.Numeric };
    private readonly Entry _reps = new() { Placeholder = "4 előre; 4 hátra" };
    private readonly Entry _estimate = new() { Placeholder = "Becsült idő másodpercben", Keyboard = Keyboard.Numeric };
    private readonly Entry _equipment = new() { Placeholder = "Eszközök vesszővel (vagy üresen)" };
    private readonly Editor _summary = new() { Placeholder = "Rövid végrehajtási leírás", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _position = new() { Placeholder = "Kiinduló helyzet", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _steps = new() { Placeholder = "Végrehajtási lépések (soronként)", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _mistakes = new() { Placeholder = "Gyakori hibák (soronként)", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Editor _safety = new() { Placeholder = "Óvintézkedések, abbahagyás feltételei", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly VerticalStackLayout _content = UI.Stack();
    private readonly Label _figureInfo = UI.Label("",13,color:UI.Muted);
    private readonly bool _open3dImmediately;
    private bool _opened3d;

    public ExerciseEditorPage(AppState state, Exercise? current = null, bool open3dImmediately=false)
    {
        _state = state; _open3dImmediately=open3dImmediately;
        _draft = current is null ? new Exercise
        {
            Id = "user-" + Guid.NewGuid().ToString("N"),
            Phase = "main", Measure = "reps", EstimatedSeconds = 45,
            Difficulty = 1, Pose = new MotionPose()
        } : ExerciseLibraryRules.Clone(current);
        ExerciseLibraryRules.Normalize(_draft);
        if (state.BuiltInCatalog.Exercises.Any(x=>x.Id==_draft.Id))
            throw new InvalidOperationException("Alapgyakorlat nem szerkeszthető.");
        UI.Setup(this,current is null ? "Új gyakorlat" : "Saját gyakorlat szerkesztése");
        foreach (var c in ExerciseLibraryRules.Categories) _category.Items.Add(c);
        foreach (var p in new[] { "Bemelegítés", "Fő rész", "Levezetés" }) _phase.Items.Add(p);
        foreach (var d in new[] { "Kezdő", "Középhaladó", "Haladó" }) _difficulty.Items.Add(d);
        _measure.Items.Add("Ismétlés"); _measure.Items.Add("Idő");
        _name.Text = _draft.Name;
        _category.SelectedIndex = Array.IndexOf(ExerciseLibraryRules.Categories,_draft.Category);
        _regions.Text = string.Join(", ",_draft.Regions);
        foreach (var pair in _draft.RegionGroups) _regionGroupChoices[pair.Key] = pair.Value;
        _regions.TextChanged += (_, _) => RefreshRegionGroups();
        _phase.SelectedIndex = Array.IndexOf(ExerciseLibraryRules.Phases,_draft.Phase);
        _difficulty.SelectedIndex = Math.Clamp(_draft.Difficulty-1,0,2);
        _measure.SelectedIndex = _draft.Measure=="time"?1:0;
        _seconds.Text = _draft.TargetSeconds?.ToString() ?? "";
        _reps.Text = string.Join("; ",_draft.RepTargets.Select(r=>$"{r.Count} {r.Label}"));
        _estimate.Text = _draft.EstimatedSeconds.ToString();
        _equipment.Text = string.Join(", ",_draft.Equipment);
        _summary.Text = _draft.Summary; _position.Text = _draft.StartingPosition;
        _steps.Text = string.Join("\n",_draft.Steps); _mistakes.Text=string.Join("\n",_draft.CommonMistakes);
        _safety.Text = _draft.SafetyNote;
        Fill();
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_open3dImmediately || _opened3d) return;
        _opened3d=true;
        Dispatcher.Dispatch(async () => await Navigation.PushAsync(new Rig3DEditorPage(_draft.Pose, pose =>
        {
            _draft.Pose=pose;
            UpdateFigureInfo();
        })));
    }
    private static List<string> Csv(string? text) => (text??"").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static List<string> Lines(string? text) => (text??"").Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();
    private static View Field(string label,View input) => new VerticalStackLayout { Spacing=4,
        Children = { UI.Label(label,13,true,color:UI.Muted),input } };
    private void Fill()
    {
        _content.Children.Add(UI.Label("Saját gyakorlat",26,true));
        _content.Children.Add(UI.Label("Piszkozatot hiányos adatokkal is menthetsz. A gyakorlatsor-generátor csak teljes, aktív gyakorlatot használ.",13,color:UI.Muted));
        _content.Children.Add(UI.Label("Alapadatok",20,true));
        _content.Children.Add(Field("Gyakorlat neve *",_name));
        _content.Children.Add(Field("Mozgástípus *",_category));
        _content.Children.Add(Field("Érintett testtájak * (vesszővel)",_regions));
        _content.Children.Add(_newRegionGroups);
        RefreshRegionGroups();
        _content.Children.Add(Field("Edzésfázis *",_phase));
        _content.Children.Add(Field("Nehézség *",_difficulty));
        _content.Children.Add(Field("Mérés típusa *",_measure));
        _content.Children.Add(Field("Időtartam (időalapú feladatoknál) *",_seconds));
        _content.Children.Add(Field("Ismétléscélok (pl. 4 előre; 4 hátra) *",_reps));
        _content.Children.Add(Field("Becsült idő másodpercben *",_estimate));
        _content.Children.Add(Field("Szükséges eszközök (eszközmentesnél üres)",_equipment));
        _content.Children.Add(UI.Label("Leírás és biztonság",20,true));
        _content.Children.Add(Field("Rövid leírás vagy végrehajtási lépések *",_summary));
        _content.Children.Add(Field("Kiinduló helyzet",_position));
        _content.Children.Add(Field("Végrehajtási lépések (soronként)",_steps));
        _content.Children.Add(Field("Gyakori hibák",_mistakes));
        _content.Children.Add(Field("Biztonsági megjegyzés",_safety));
        _content.Children.Add(UI.Label("Ábra (opcionális)",20,true));
        _content.Children.Add(_figureInfo);
        var draw=UI.Button("✎ 3D csontváz és eszközök szerkesztése",false);
        draw.Clicked+=async (_,_)=>{
            await Navigation.PushAsync(new Rig3DEditorPage(_draft.Pose, pose=>
            { _draft.Pose=pose; UpdateFigureInfo(); }));
        };
        _content.Children.Add(draw);
        var saveDraft=UI.Button("Piszkozat mentése",false);
        saveDraft.Clicked+=async (_,_)=> await Save(LibraryStatuses.Draft);
        var activate=UI.Button("Aktiválás és mentés");
        activate.Clicked+=async (_,_)=> await Save(LibraryStatuses.Active);
        var cancel=UI.Button("Mégse",false);
        cancel.Clicked+=async (_,_)=> await Navigation.PopModalAsync();
        var buttons=new VerticalStackLayout {Spacing=6,Children={activate,saveDraft,cancel}};
        var layout=new Grid {RowDefinitions={new RowDefinition{Height=GridLength.Star},new RowDefinition{Height=GridLength.Auto}}};
        var scroll=new ScrollView{Content=_content};layout.Add(scroll,0,0);
        var footer=new Border {BackgroundColor=UI.Background,Padding=new Thickness(18,8,18,16),Content=buttons};
        layout.Add(footer,0,1);Content=layout;UpdateFigureInfo();
    }
    private void UpdateFigureInfo() => _figureInfo.Text=_draft.Pose.Rig3D is null
        ? "Nincs 3D ábra; ez nem akadálya az aktiválásnak."
        : $"{_draft.Pose.Rig3D.Frames.Count} 3D fázis · preset: {_draft.Pose.Rig3D.Preset}";

    private void RefreshRegionGroups()
    {
        foreach (var (region, picker) in _regionGroupPickers)
            if (picker.SelectedIndex >= 0)
                _regionGroupChoices[region] = picker.SelectedIndex == 0
                    ? RegionClassification.Upper : RegionClassification.Lower;
        _regionGroupPickers.Clear();
        _newRegionGroups.Children.Clear();
        foreach (var region in Csv(_regions.Text).Where(r => RegionClassification.BuiltInGroup(r) is null))
        {
            var picker = new Picker { Title = "Válassz testtájcsoportot *" };
            picker.Items.Add(RegionClassification.Upper);
            picker.Items.Add(RegionClassification.Lower);
            if (_regionGroupChoices.TryGetValue(region, out var chosen))
                picker.SelectedIndex = chosen == RegionClassification.Upper ? 0
                    : chosen == RegionClassification.Lower ? 1 : -1;
            _regionGroupPickers[region] = picker;
            _newRegionGroups.Children.Add(Field($"Új testtáj: {region} – besorolás *", picker));
        }
    }

    private void Gather()
    {
        _draft.Name=_name.Text?.Trim()??"";
        _draft.Category=_category.SelectedItem?.ToString()??"";
        _draft.Regions=Csv(_regions.Text);
        foreach (var (region, picker) in _regionGroupPickers)
            if (picker.SelectedIndex >= 0)
                _regionGroupChoices[region] = picker.SelectedIndex == 0
                    ? RegionClassification.Upper : RegionClassification.Lower;
        _draft.RegionGroups = _draft.Regions
            .Where(r => RegionClassification.BuiltInGroup(r) is null && _regionGroupChoices.ContainsKey(r))
            .ToDictionary(r => r, r => _regionGroupChoices[r], StringComparer.OrdinalIgnoreCase);
        _draft.Phase=(_phase.SelectedIndex>=0 && _phase.SelectedIndex<3)?ExerciseLibraryRules.Phases[_phase.SelectedIndex]:"";
        _draft.Difficulty=_difficulty.SelectedIndex+1;
        _draft.Measure=_measure.SelectedIndex==1?"time":"reps";
        _draft.TargetSeconds = _draft.Measure=="time" && int.TryParse(_seconds.Text,out var sec)?sec:null;
        _draft.RepTargets=[];
        if(_draft.Measure=="reps")
        {
            foreach(var (piece,i) in (_reps.Text??"").Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select((x,i)=>(x,i)))
            {
                var parts=piece.Split(' ',2,StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length>=1 && int.TryParse(parts[0],out var count))
                    _draft.RepTargets.Add(new RepTarget {Code=$"segment-{i}",Count=count,
                        Label=parts.Length>1?parts[1]:"ismétlés"});
            }
        }
        _draft.EstimatedSeconds=int.TryParse(_estimate.Text,out var estimated)?estimated:0;
        _draft.Equipment=Csv(_equipment.Text);
        _draft.Summary=_summary.Text?.Trim()??"";
        _draft.StartingPosition=_position.Text?.Trim()??"";
        _draft.Steps=Lines(_steps.Text);
        _draft.CommonMistakes=Lines(_mistakes.Text);
        _draft.SafetyNote=_safety.Text?.Trim()??"";
    }
    private async Task Save(string status)
    {
        Gather();
        var missingGroups = _draft.Regions.Where(r => RegionClassification.BuiltInGroup(r) is null &&
            (!_draft.RegionGroups.TryGetValue(r, out var group) ||
             group is not (RegionClassification.Upper or RegionClassification.Lower))).ToList();
        if (missingGroups.Count > 0)
        {
            await DisplayAlertAsync("Testtáj besorolása szükséges",
                "Az új testtájaknál válassz Felsőtest vagy Alsótest csoportot: " + string.Join(", ", missingGroups),
                "Rendben");
            return;
        }
        if(status==LibraryStatuses.Active)
        {
            var validation=ExerciseLibraryRules.ValidateForActivation(_draft);
            if(!validation.IsValid)
            {await DisplayAlertAsync("Még hiányoznak adatok",string.Join("\n",validation.Errors),"Rendben");return;}
        }
        try
        {
            await _state.SaveCustomExerciseAsync(_draft,status);
            await Navigation.PopModalAsync();
        }
        catch(Exception ex){await DisplayAlertAsync("Mentési hiba",ex.Message,"Rendben");}
    }
}
