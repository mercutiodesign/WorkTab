﻿// Karel Kroeze
// PawnColumnWorker_WorkType.cs
// 2017-05-22

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static WorkTab.Constants;
using static WorkTab.InteractionUtilities;
using static WorkTab.MainTabWindow_WorkTab;
using static WorkTab.Resources;

namespace WorkTab
{
    public class PawnColumnWorker_WorkType : PawnColumnWorker, IAlternatingColumn, IExpandableColumn
    {
        public override int GetMinWidth( PawnTable table ) { return WorkTypeWidth; }

        private bool _moveDown;

        public bool MoveDown
        {
            get { return _moveDown; }
            set { _moveDown = value; }
        }

        public bool IncapableOfWholeWorkType( Pawn pawn )
        {
            return !def.workType.workGiversByPriority.Any( wg => pawn.CapableOf( wg ) );
        }

        public override void DoCell( Rect rect, Pawn pawn, PawnTable table )
        {
            // bail out if showing worksettings is nonsensical
            if ( pawn.Dead || !pawn.workSettings.EverWork )
                return;

            var incapable = IncapableOfWholeWorkType( pawn );
            var worktype = def.workType;

            // create rect in centre of cell
            var pos = rect.center - new Vector2( WorkTypeBoxSize, WorkTypeBoxSize ) / 2f;
            var box = new Rect( pos.x, pos.y, WorkTypeBoxSize, WorkTypeBoxSize );

            // plop in the tooltip
            Func<string> tipGetter = delegate { return DrawUtilities.TipForPawnWorker( pawn, worktype, incapable ); };
            TooltipHandler.TipRegion( box, tipGetter, pawn.thingIDNumber ^ worktype.GetHashCode() );

            // bail out if worktype is disabled (or pawn has no background story).
            if ( !ShouldDrawCell( pawn ) )
                return;

            // draw the workbox
            Text.Font = GameFont.Medium;
            DrawWorkTypeBoxFor( box, pawn, worktype, incapable );
            Text.Font = GameFont.Small;

            // handle interactions
            HandleInteractions( rect, pawn );
        }

        protected virtual void DrawWorkTypeBoxFor( Rect box, Pawn pawn, WorkTypeDef worktype, bool incapable )
        {
            // draw background
            GUI.color = incapable ? new Color( 1f, .3f, .3f ) : Color.white;
            DrawUtilities.DrawWorkBoxBackground( box, pawn, worktype );
            GUI.color = Color.white;

            // draw extras
            var tracker = PriorityManager.Get[pawn];
            if (tracker.TimeScheduled(worktype))
                DrawUtilities.DrawTimeScheduled(box);
            if (tracker.PartScheduled(worktype))
                DrawUtilities.DrawPartScheduled(box);

            // draw priorities / checks
            DrawUtilities.DrawPriority( box, pawn.GetPriority( worktype, VisibleHour ) );
        }

        protected virtual void HandleInteractions( Rect rect, Pawn pawn )
        {
            if ( Find.PlaySettings.useWorkPriorities )
                HandleInteractionsDetailed( rect, pawn );
            else
                HandleInteractionsToggle( rect, pawn );
        }

        private void HandleInteractionsDetailed( Rect rect, Pawn pawn )
        {
            if ( ( Event.current.type == EventType.MouseDown || Event.current.type == EventType.ScrollWheel )
                 && Mouse.IsOver( rect ) )
            {
                // track priority so we can play appropriate sounds
                int oldpriority = pawn.GetPriority( def.workType, VisibleHour );

                // deal with clicks and scrolls
                if ( ScrolledUp( rect, true ) || RightClicked( rect ) )
                    def.workType.IncrementPriority( pawn, VisibleHour, SelectedHours );
                if ( ScrolledDown( rect, true ) || LeftClicked( rect ) )
                    def.workType.DecrementPriority( pawn, VisibleHour, SelectedHours );

                // get new priority, play crunch if it wasn't pretty
                int newPriority = pawn.GetPriority( def.workType, -1 );
                if ( Settings.playSounds && Settings.playCrunch &&
                     oldpriority == 0 && newPriority > 0 &&
                     pawn.skills.AverageOfRelevantSkillsFor( def.workType ) <= 2f )
                {
                    SoundDefOf.Crunch.PlayOneShotOnCamera();
                }

                // update tutorials
                PlayerKnowledgeDatabase.KnowledgeDemonstrated( ConceptDefOf.WorkTab, KnowledgeAmount.SpecificInteraction );
                PlayerKnowledgeDatabase.KnowledgeDemonstrated( ConceptDefOf.ManualWorkPriorities,
                                                               KnowledgeAmount.SmallInteraction );
            }
        }

        public bool ShouldDrawCell( Pawn pawn )
        {
            if ( pawn?.story == null )
                return false;

            return !pawn.story.WorkTypeIsDisabled( def.workType );
        }

        public List<Pawn> CapablePawns => Instance
            .Table
            .PawnsListForReading
            .Where( ShouldDrawCell )
            .ToList();

        private void HandleInteractionsToggle( Rect rect, Pawn pawn )
        {
            if ( ( Event.current.type == EventType.MouseDown || Event.current.type == EventType.ScrollWheel )
                 && Mouse.IsOver( rect ) )
            {
                // track priority so we can play appropriate sounds
                bool active = pawn.GetPriority( def.workType, VisibleHour ) > 0;

                if ( active )
                {
                    pawn.SetPriority( def.workType, 0, SelectedHours );
                    if (Settings.playSounds)
                        SoundDefOf.CheckboxTurnedOff.PlayOneShotOnCamera();
                }
                else
                {
                    pawn.SetPriority( def.workType, Mathf.Min( Settings.maxPriority, 3 ), SelectedHours );
                    if (Settings.playSounds)
                        SoundDefOf.CheckboxTurnedOn.PlayOneShotOnCamera();
                    if (Settings.playSounds && Settings.playCrunch && pawn.skills.AverageOfRelevantSkillsFor( def.workType ) <= 2f )
                    {
                        SoundDefOf.Crunch.PlayOneShotOnCamera();
                    }
                }

                // stop event propagation, update tutorials
                Event.current.Use();
                PlayerKnowledgeDatabase.KnowledgeDemonstrated( ConceptDefOf.WorkTab, KnowledgeAmount.SpecificInteraction );
            }
        }


        protected override void HeaderClicked( Rect headerRect, PawnTable table )
        {
            // replaced with HeaderInteractions
        }

        public override void DoHeader( Rect rect, PawnTable table )
        {
            // make sure we're at the correct font size
            Text.Font = GameFont.Small;
            Rect labelRect = rect;

            if ( Settings.verticalLabels )
                DrawVerticalHeader( rect, table );
            else
                DrawHorizontalHeader( rect, table, out labelRect );

            // handle interactions (click + scroll)
            HeaderInteractions( labelRect, table );

            // mouseover stuff
            Widgets.DrawHighlightIfMouseover( labelRect );
            TooltipHandler.TipRegion( labelRect, GetHeaderTip( table ) );

            // sort icon
            if ( table.SortingBy == def )
            {
                Texture2D sortIcon = ( !table.SortingDescending ) ? SortingIcon : SortingDescendingIcon;
                Rect bottomRight = new Rect( rect.xMax - sortIcon.width - 1f, rect.yMax - sortIcon.height - 1f,
                                             sortIcon.width, sortIcon.height );
                GUI.DrawTexture( bottomRight, sortIcon );
            }
        }

        public void DrawVerticalHeader( Rect rect, PawnTable table )
        {
            GUI.color = new Color(.8f, .8f, .8f);
            Text.Anchor = TextAnchor.MiddleLeft;
            DrawUtilities.VerticalLabel(rect, def.workType.labelShort.Truncate(rect.height, VerticalTruncationCache));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        public void DrawHorizontalHeader( Rect rect, PawnTable table, out Rect labelRect )
        {
            // get offset rect
            labelRect = GetLabelRect(rect);

            // draw label
            Widgets.Label(labelRect, def.workType.labelShort.Truncate(labelRect.width, TruncationCache));

            // get bottom center of label
            var start = new Vector2(labelRect.center.x, labelRect.yMax);
            var length = rect.yMax - start.y;

            // make sure we're not at a whole pixel
            if (start.x - (int)start.x < 1e-4)
                start.x += .5f;

            // draw the lines - two separate lines give a clearer edge than one 2px line...
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            Widgets.DrawLineVertical(Mathf.FloorToInt(start.x), start.y, length);
            Widgets.DrawLineVertical(Mathf.CeilToInt(start.x), start.y, length);
            GUI.color = Color.white;
        }

        public override int Compare( Pawn a, Pawn b )
        {
            return GetValueToCompare( a ).CompareTo( GetValueToCompare( b ) );
        }

        private string _headerTip;

        protected override string GetHeaderTip( PawnTable table )
        {
            if ( _headerTip.NullOrEmpty() )
                _headerTip = CreateHeaderTip( table );
            return _headerTip;
        }

        private string CreateHeaderTip( PawnTable table )
        {
            string tip = "";
            tip += def.workType.gerundLabel + "\n\n";
            tip += def.workType.description + "\n\n";
            tip += def.workType.SpecificWorkListString() + "\n";
            tip += "\n" + "ClickToSortByThisColumn".Translate();
            if ( Find.PlaySettings.useWorkPriorities )
                tip += "\n" + "WorkTab.DetailedColumnTip".Translate();
            else
                tip += "\n" + "WorkTab.ToggleColumnTip".Translate();
            if ( CanExpand )
                tip += "\n" + "WorkTab.ExpandWorkgiversColumnTip".Translate();
            return tip;
        }

        private float GetValueToCompare( Pawn pawn )
        {
            if ( pawn.workSettings == null || !pawn.workSettings.EverWork )
            {
                return -2f;
            }
            if ( pawn.story != null && pawn.story.WorkTypeIsDisabled( this.def.workType ) )
            {
                return -1f;
            }

            return pawn.skills.AverageOfRelevantSkillsFor( this.def.workType );
        }


        private Vector2 cachedLabelSize = Vector2.zero;

        public Vector2 LabelSize
        {
            get
            {
                if ( cachedLabelSize == Vector2.zero )
                {
                    cachedLabelSize = Text.CalcSize( def.workType.labelShort );
                }
                return cachedLabelSize;
            }
        }

        public override int GetMinHeaderHeight( PawnTable table ) { return Settings.verticalLabels ? VerticalHeaderHeight : HorizontalHeaderHeight; }

        private Rect GetLabelRect( Rect headerRect )
        {
            float x = headerRect.center.x;
            Rect result = new Rect( x - LabelSize.x / 2f, headerRect.y, LabelSize.x, LabelSize.y );
            if ( MoveDown )
                result.y += 20f;
            return result;
        }

        public void HeaderInteractions( Rect rect, PawnTable table )
        {
            if ( !Mouse.IsOver( rect ) )
                return;

            // handle interactions
            if ( Event.current.shift )
            {
                // deal with clicks and scrolls
                if ( Find.PlaySettings.useWorkPriorities )
                {
                    if ( ScrolledUp( rect, true ) || RightClicked( rect ) )
                        def.workType.IncrementPriority( CapablePawns, VisibleHour, SelectedHours );
                    if ( ScrolledDown( rect, true ) || LeftClicked( rect ) )
                        def.workType.DecrementPriority( CapablePawns, VisibleHour, SelectedHours );
                }
                else
                {
                    // this gets slightly more complicated
                    var pawns = CapablePawns;
                    if ( ScrolledUp( rect, true ) || RightClicked( rect ) )
                    {
                        if ( pawns.Any( p => p.GetPriority( def.workType, VisibleHour ) != 0 ) )
                        {
                            if (Settings.playSounds)
                                SoundDefOf.CheckboxTurnedOff.PlayOneShotOnCamera();
                            foreach ( Pawn pawn in pawns )
                                pawn.SetPriority( def.workType, 0, SelectedHours );
                        }
                    }

                    if ( ScrolledDown( rect, true ) || LeftClicked( rect ) )
                    {
                        if ( pawns.Any( p => p.GetPriority( def.workType, VisibleHour ) == 0 ) )
                        {
                            if (Settings.playSounds)
                                SoundDefOf.CheckboxTurnedOn.PlayOneShotOnCamera();
                            foreach ( Pawn pawn in pawns )
                                pawn.SetPriority( def.workType, 3, SelectedHours );
                        }
                    }
                }
            }
            else if ( Event.current.control )
            {
                if ( CanExpand && Clicked( rect ) )
                    Expanded = !Expanded;
            }
            else if ( def.sortable )
            {
                if ( LeftClicked( rect ) )
                    Sort( table );
                if ( RightClicked( rect ) )
                    Sort( table, false );
            }
        }

        protected virtual void Sort( PawnTable table, bool ascending = true )
        {
            if ( ascending )
            {
                if ( table.SortingBy != def )
                {
                    table.SortBy( def, true );
                    SoundDefOf.TickHigh.PlayOneShotOnCamera();
                }
                else if ( table.SortingDescending )
                {
                    table.SortBy( def, false );
                    SoundDefOf.TickHigh.PlayOneShotOnCamera();
                }
                else
                {
                    table.SortBy( null, false );
                    SoundDefOf.TickLow.PlayOneShotOnCamera();
                }
            }
            else
            {
                if ( table.SortingBy != def )
                {
                    table.SortBy( def, false );
                    SoundDefOf.TickHigh.PlayOneShotOnCamera();
                }
                else if ( table.SortingDescending )
                {
                    table.SortBy( null, false );
                    SoundDefOf.TickLow.PlayOneShotOnCamera();
                }
                else
                {
                    table.SortBy( def, true );
                    SoundDefOf.TickHigh.PlayOneShotOnCamera();
                }
            }
        }

        #region Implementation of IExpandableColumn

        private bool _expanded = false;

        public bool Expanded
        {
            get { return _expanded; }
            set
            {
                _expanded = value;
                RebuildTable();
            }
        }

        public bool CanExpand => ChildColumns.Count > 1;

        public List<PawnColumnDef> ChildColumns
        {
            get
            {
                // get all the columns
                var columns = DefDatabase<PawnColumnDef>
                    .AllDefsListForReading
                    .OfType<PawnColumnDef_WorkGiver>()
                    .Where( c => c != null && def.workType.workGiversByPriority.Contains( c.workgiver ) );

                // sort if needed (and possible).
                if ( columns.Count() > 1 )
                    columns = columns.OrderByDescending( c => c.workgiver.priorityInType );

                // cast back to list of vanilla defs
                return columns
                    .Cast<PawnColumnDef>()
                    .ToList();
            }
        }

        #endregion
    }
}
