﻿// Karel Kroeze
// VanillaWorkSettings.cs
// 2017-05-22

using System;
using System.Reflection;
using Harmony;
using RimWorld;
using Verse;

namespace WorkTab
{
    public static class VanillaWorkSettings
    {
        private static FieldInfo prioritiesFieldInfo;
        private static FieldInfo pawnFieldInfo;

        public static int GetVanillaPriority( this Pawn pawn, WorkTypeDef worktype )
        {
            if ( prioritiesFieldInfo == null )
            {
                prioritiesFieldInfo = typeof( Pawn_WorkSettings ).GetField( "priorities", AccessTools.all );
                if ( prioritiesFieldInfo == null )
                    throw new NullReferenceException( "priorities field not found" );
            }

            int priority;
            try
            {
                priority = ( prioritiesFieldInfo.GetValue( pawn.workSettings ) as DefMap<WorkTypeDef, int> )[worktype];
            }
            catch ( ArgumentOutOfRangeException )
            {
                priority = 0; 
                Logger.Message( $"Priority requested for a workgiver that did not yet exist for {pawn.NameStringShort}. Did you add mods in an existing game?" );
            }
            return priority;
        }

        public static Pawn Pawn( this Pawn_WorkSettings worksettings )
        {
            if (pawnFieldInfo == null)
            {
                pawnFieldInfo = typeof(Pawn_WorkSettings).GetField("pawn", AccessTools.all);
                if (pawnFieldInfo == null)
                    throw new NullReferenceException("could not get pawn field");
            }

            return pawnFieldInfo.GetValue(worksettings) as Pawn;
        }
    }
}
