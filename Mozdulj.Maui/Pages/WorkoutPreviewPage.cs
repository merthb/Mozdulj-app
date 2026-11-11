using Mozdulj.Core;
using Mozdulj.Maui.Services;
namespace Mozdulj.Maui.Pages;

public sealed class WorkoutPreviewPage : ContentPage
{
    private readonly AppState _state;
    private readonly Workout _workout;
    private readonly VerticalStackLayout _content = UI.Stack();
    private readonly List<ReorderCard> _reorderCards = new();
    private bool _reorderBusy;
    private sealed record ReorderCard(string Id, string Phase, Border Card);
    public WorkoutPreviewPage(AppState state, Workout workout)
    {
        _state = state; _workout = workout;
        UI.Setup(this, "Gyakorlatsor");
        Content = new ScrollView { Content = _content };
    }
    protected override void OnAppearing() { base.OnAppearing(); Draw(); }
    private void Draw()
    {
        _reorderCards.Clear();
        _content.Children.Clear();
        _content.Children.Add(UI.Label(_workout.Title, 25, true));
        _content.Children.Add(UI.Label(_workout.Kind == "recovery" ? "10 perc · folyamatos relaxáció" : $"{_workout.Items.Count} feladat · {(_workout.TargetMinutes>0?_workout.TargetMinutes:_state.Preferences.WorkoutMinutes)} perc cél · kb. {Math.Ceiling(_workout.EstimatedSeconds/60.0)} perc az átvezetésekkel", 14, color:UI.Muted));
        if (_workout.Kind == "recovery")
        {
            // Egyetlen rövid blokk: az instrukció csak itt szerepel, nem három helyen.
            // Mindig az aktuális szöveget használjuk a korábban mentett programoknál is.
            _content.Children.Add(UI.Card(
                UI.Label("🌿 Ellazulás", 17, true),
                UI.Label(RecoveryRoutine.Guidance, 14, color:UI.Muted),
                UI.Label("10 perc után megőrzi a sorozatot · 0 XP", 12, color:UI.Muted)));
        }
        else
        {
            _content.Children.Add(UI.Label($"Bemelegítés: {_workout.Items.Count(x=>x.Phase=="warmup")} · Fő rész: {_workout.Items.Count(x=>x.Phase=="main")} · Levezetés: {_workout.Items.Count(x=>x.Phase=="cooldown")}",14,true));
            if(!string.IsNullOrWhiteSpace(_workout.PlanningNote))
                _content.Children.Add(UI.Card(UI.Label(_workout.PlanningNote,13,color:UI.Muted)));
            _content.Children.Add(UI.Label(_workout.Status == "planned"
                ? "A leíráshoz koppints a gyakorlat nevére. Átrendezéshez fogd meg a bal oldali háromvonalas ikont, és húzd a gyakorlatot a kívánt helyre ugyanabban a blokkban."
                : "A gyakorlat nevére koppintva megnyithatod a részleteket.",
                13, color:UI.Muted));
            if (_workout.Status=="planned")
                _content.Children.Add(UI.Label($"Kihagyható feladatok: {WorkoutPlanEditing.RemainingRemovals(_workout)} / {WorkoutPlanEditing.MaxRemovals(_workout.TargetMinutes)}",13,true,color:UI.Teal));
            if (_workout.Status != "planned")
                _content.Children.Add(UI.Label("🔒 A gyakorlatsor már elindult, ezért a feladatok nem módosíthatók.",12,color:UI.Muted));
            if(_workout.Status=="planned")
            {
                _content.Children.Add(UI.Label("Kötelező gyakorlatok: kijelölhetsz olyan feladatokat, amelyek biztosan szerepeljenek a mai sorban. A program a fennmaradó részt újratervezi az időkeret és az edzésfázisok szerint. Csak a sor elindítása előtt használható.",12,color:UI.Muted));
                var must=UI.SubtleAction("★ Kötelezők kiválasztása ›");
                must.Clicked+=async (_,_)=>await Navigation.PushAsync(new WorkoutMandatoryPage(_state,_workout));
                _content.Children.Add(must);
            }
        }
        string? previousPhase=null;
        if (_workout.Kind != "recovery")
        foreach (var item in _workout.Items)
        {
            var it = item;
            async Task ShowDetailsAsync()
            {
                var exercise = _state.FindExercise(it.ExerciseId);
                if (exercise != null) await Navigation.PushAsync(new ExerciseDetailPage(_state, exercise));
                else await DisplayAlert("Korábbi gyakorlat", "Ez a gyakorlat nincs az aktuális katalógusban.", "Rendben");
            }
            string phase;
            if (_workout.Kind == "recovery") phase = "recovery";
            else try { phase = WorkoutCustomization.ResolvePhase(_state.Catalog, it); }
            catch (InvalidOperationException) { phase = "unknown"; }
            if (phase!=previousPhase)
            {
                _content.Children.Add(UI.Label(phase switch
                {"warmup"=>"1. Bemelegítés","main"=>"2. Fő blokk","cooldown"=>"3. Levezetés","recovery"=>"Ellazulási szakaszok",_=>"Feladatok"},19,true));
                previousPhase=phase;
            }
            // The drag handle occupies its own touch target at the far left.
            // Only the middle text area opens the details; the right action swaps.
            // There are no per-exercise numbers in this view.
            var canReorder = _workout.Status == "planned";
            var row = new Grid { ColumnSpacing = 6 };
            if (canReorder)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var textColumn = canReorder ? 1 : 0;
            var actionColumn = textColumn + 1;

            Border? dragHandle = null;
            if (canReorder)
            {
                // Three real bars rather than a font glyph: consistent on Android.
                // The 42x54 dp surface is larger than the visible symbol, so it is
                // easy to grab without accidentally opening the exercise details.
                var bars = new VerticalStackLayout
                {
                    Spacing = 4,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    InputTransparent = true
                };
                for (var bar = 0; bar < 3; bar++)
                    bars.Children.Add(new BoxView
                    {
                        Color = UI.Purple,
                        WidthRequest = 19,
                        HeightRequest = 3,
                        InputTransparent = true
                    });

                var handle = new Border
                {
                    Content = bars,
                    WidthRequest = 42,
                    HeightRequest = 54,
                    Padding = 0,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Start,
                    BackgroundColor = UI.Lavender,
                    StrokeThickness = 0,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                    { CornerRadius = new CornerRadius(10) }
                };
                SemanticProperties.SetDescription(handle, $"{it.Name}: átrendezés");
                SemanticProperties.SetHint(handle, "Fogd meg, és húzd egy másik gyakorlatra ugyanebben a blokkban.");
                dragHandle = handle;
#if !ANDROID
                // Other platforms can keep the standard MAUI drag-and-drop path.
                var drag = new DragGestureRecognizer { CanDrag = true };
                drag.DragStarting += (_, args) => args.Data.Text = it.Id;
                handle.GestureRecognizers.Add(drag);
#endif
                Grid.SetColumn(handle, 0);
                row.Children.Add(handle);
            }

            var heading = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            heading.Children.Add(UI.Label(it.Name + (it.IsRequired ? " ★ Kötelező" : ""), 16, true));
            var phaseLabel = phase switch
            {
                "warmup" => "Bemelegítés", "main" => "Fő rész", "cooldown" => "Levezetés", "recovery" => "Relaxáció", _ => "Ismeretlen fázis"
            };
            heading.Children.Add(UI.Label($"{phaseLabel} · {it.Category} · {it.TargetLabel} {(it.Status == "completed" ? "✓" : "")}", 13, color:UI.Muted));
            var exerciseDefinition = _state.FindExercise(it.ExerciseId);
            if (exerciseDefinition?.Equipment.Count > 0)
                heading.Children.Add(UI.Label("🎒 Szükséges: " + exerciseDefinition.EquipmentLabel, 13, true, color:UI.Purple));
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowDetailsAsync();
            heading.GestureRecognizers.Add(tap);
            Grid.SetColumn(heading, textColumn);
            row.Children.Add(heading);
            if (canReorder && it.Status == "pending" && !it.IsRequired && phase != "unknown")
            {
                var replace = UI.SubtleAction("⇄ Csere");
                replace.VerticalOptions = LayoutOptions.Center;
                replace.Clicked += async (_, _) =>
                    await Navigation.PushAsync(new WorkoutReplacementPage(_state, _workout, it));
                Grid.SetColumn(replace, actionColumn);
                row.Children.Add(replace);
            }
            var card=UI.Card(row);
            if (_workout.Status == "planned")
            {
                _reorderCards.Add(new ReorderCard(it.Id, phase, card));
#if ANDROID
                if (dragHandle != null)
                    InstallAndroidReorderHandle(dragHandle, card, it.Id, phase);
#else
                var drop=new DropGestureRecognizer();
                drop.Drop+=async (_,args)=>
                {
                    var dragged=await args.Data.GetTextAsync();
                    if(string.IsNullOrWhiteSpace(dragged) || dragged==it.Id)return;
                    try {await _state.MoveWorkoutItemAsync(_workout,dragged,it.Id);Draw();}
                    catch(Exception ex){await DisplayAlert("Nem rendezhető át",ex.Message,"Rendben");}
                };
                card.GestureRecognizers.Add(drop);
#endif
            }
            _content.Children.Add(card);
        }
        if (_workout.Status is "completed" or "partial" or "cancelled")
        {
            _content.Children.Add(UI.Card(UI.Label($"Lezárt gyakorlatsor · {_workout.CompletedCount} / {_workout.Items.Count} feladat", 15, true)));
        }
        else
        {
            var start=UI.Button(_workout.Status=="in_progress"?"▶ Folytatás":"▶ Kezdés");
            start.Clicked+=async (_,_) =>
            {
                if (_workout.Kind == "recovery" ? !RecoveryRoutine.IsValid(_workout) : !WorkoutCoherence.IsReadyToStart(_state.Catalog,_workout))
                {
                    await DisplayAlert("Nem megfelelő gyakorlatsor",
                        "A gyakorlatsor ellenőrzése nem sikerült. A program nem indítható el.","Rendben");
                    return;
                }
                await Navigation.PushAsync(new WorkoutPlayerPage(_state,_workout));
            };
            _content.Children.Add(start);
        }
    }

#if ANDROID
    // Android's ScrollView intercepts vertical drag gestures inside its content.
    // On the dedicated grip we capture the touch BEFORE the parent can scroll,
    // then move the card visually and resolve the drop against visible cards.
    // The rest of the row remains a normal scroll/tap surface.
    private void InstallAndroidReorderHandle(Border handle, Border sourceCard, string sourceId, string phase)
    {
        Android.Views.View? nativeHandle = null;
        EventHandler<Android.Views.View.TouchEventArgs>? touchHandler = null;
        handle.HandlerChanged += (_, _) =>
        {
            if (nativeHandle != null && touchHandler != null)
                nativeHandle.Touch -= touchHandler;
            nativeHandle = handle.Handler?.PlatformView as Android.Views.View;
            if (nativeHandle is null) return;

            var downY = 0f;
            var pressed = false;
            Border? target = null;
            var originalStroke = sourceCard.Stroke;

            void SetTarget(Border? next)
            {
                if (ReferenceEquals(target, next)) return;
                if (target != null)
                {
                    target.Stroke = Color.FromArgb("#E7E2F1");
                    target.StrokeThickness = 1;
                }
                target = next;
                if (target != null)
                {
                    target.Stroke = UI.Purple;
                    target.StrokeThickness = 3;
                }
            }

            void Restore()
            {
                sourceCard.TranslationY = 0;
                sourceCard.ZIndex = 0;
                sourceCard.Opacity = 1;
                sourceCard.Stroke = originalStroke;
                SetTarget(null);
                nativeHandle?.Parent?.RequestDisallowInterceptTouchEvent(false);
                pressed = false;
            }

            Border? FindDestination(float rawY)
            {
                Border? result = null;
                double distance = double.MaxValue;
                var tolerance = 9 * Microsoft.Maui.Devices.DeviceDisplay.MainDisplayInfo.Density;
                foreach (var row in _reorderCards)
                {
                    if (row.Id == sourceId || row.Phase != phase) continue;
                    if (row.Card.Handler?.PlatformView is not Android.Views.View view) continue;
                    var coords = new int[2];
                    view.GetLocationOnScreen(coords);
                    var top = coords[1];
                    var bottom = top + view.Height;
                    if (rawY < top - tolerance || rawY > bottom + tolerance) continue;
                    var delta = Math.Abs(rawY - (top + bottom) / 2d);
                    if (delta >= distance) continue;
                    distance = delta;
                    result = row.Card;
                }
                return result;
            }

            touchHandler = (_, args) =>
            {
                var ev = args.Event;
                if (ev == null) return;
                switch (ev.ActionMasked)
                {
                    case Android.Views.MotionEventActions.Down:
                        if (_reorderBusy || _workout.Status != "planned") return;
                        downY = ev.RawY;
                        pressed = true;
                        // This prevents the ScrollView from stealing subsequent MOVE events.
                        nativeHandle?.Parent?.RequestDisallowInterceptTouchEvent(true);
                        args.Handled = true;
                        break;
                    case Android.Views.MotionEventActions.Move:
                        if (!pressed) return;
                        args.Handled = true;
                        var density = Microsoft.Maui.Devices.DeviceDisplay.MainDisplayInfo.Density;
                        sourceCard.TranslationY = (ev.RawY - downY) / density;
                        sourceCard.ZIndex = 10;
                        sourceCard.Opacity = 0.82;
                        SetTarget(FindDestination(ev.RawY));
                        break;
                    case Android.Views.MotionEventActions.Up:
                        if (!pressed) return;
                        args.Handled = true;
                        var selected = _reorderCards.FirstOrDefault(x => ReferenceEquals(x.Card, target))?.Id;
                        Restore();
                        if (selected != null && selected != sourceId)
                        {
                            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(
                                async () => await ApplyReorderAsync(sourceId, selected));
                        }
                        break;
                    case Android.Views.MotionEventActions.Cancel:
                        if (!pressed) return;
                        args.Handled = true;
                        Restore();
                        break;
                }
            };
            nativeHandle.Touch += touchHandler;
        };
        handle.HandlerChanging += (_, args) =>
        {
            if (nativeHandle != null && touchHandler != null)
                nativeHandle.Touch -= touchHandler;
            nativeHandle = null;
        };
    }

    private async Task ApplyReorderAsync(string sourceId, string destinationId)
    {
        if (_reorderBusy || _workout.Status != "planned") return;
        _reorderBusy = true;
        try
        {
            await _state.MoveWorkoutItemAsync(_workout, sourceId, destinationId);
            Draw();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Nem rendezhető át", ex.Message, "Rendben");
        }
        finally
        {
            _reorderBusy = false;
        }
    }
#endif
}
