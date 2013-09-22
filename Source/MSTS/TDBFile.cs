﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using System.IO;
#if NEW_SIGNALLING
using ORTS;
using System.Collections.Generic;
#endif

namespace MSTS
{
    /// <summary>
    /// Summary description for TDBFile.
    /// </summary>
    /// 

 
    public class TDBFile
    {
        public TDBFile(string filenamewithpath)
        {
            using (STFReader stf = new STFReader(filenamewithpath, false))
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("trackdb", ()=>{ TrackDB = new TrackDB(stf); }),
                });
        }

        /// <summary>
        /// Provide a link to the TrJunctionNode for the switch track with 
        /// the specified UiD on the specified tile.
        /// 
        /// Called by switch track shapes to determine the correct position of the points.
        /// </summary>
        /// <param name="tileX"></param>
        /// <param name="tileZ"></param>
        /// <param name="UiD"></param>
        /// <returns></returns>
        public TrJunctionNode GetTrJunctionNode(int tileX, int tileZ, int UiD)
        {
            foreach (TrackNode tn in TrackDB.TrackNodes)
                if (tn != null && tn.TrJunctionNode != null)
                    if (tileX == tn.UiD.WorldTileX && tileZ == tn.UiD.WorldTileZ && UiD == tn.UiD.WorldID)
                        return tn.TrJunctionNode;

            Trace.TraceWarning("{{TileX:{0} TileZ:{1}}} track node {2} could not be found in TDB", tileX, tileZ, UiD);
            return null;
        }

        public TrackDB TrackDB;  // Warning, the first TDB entry is always null
    }



    public class TrackDB
    {
        public TrackDB(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tracknodes", ()=>{
                    stf.MustMatch("(");
                    int count = stf.ReadInt(null);
                    TrackNodes = new TrackNode[count + 1];
                    int idx = 1;
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("tracknode", ()=>{ TrackNodes[idx] = new TrackNode(stf, idx, count); ++idx; }),
                    });
                }),
                new STFReader.TokenProcessor("tritemtable", ()=>{
                    stf.MustMatch("(");
                    int count = stf.ReadInt(null);
                    TrItemTable = new TrItem[count];
                    int idx = -1;
                    stf.ParseBlock(()=> ++idx == -1, new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("crossoveritem", ()=>{ TrItemTable[idx] = new CrossoverItem(stf,idx); }),
                        new STFReader.TokenProcessor("signalitem", ()=>{ TrItemTable[idx] = new SignalItem(stf,idx); }),
                        new STFReader.TokenProcessor("speedpostitem", ()=>{ TrItemTable[idx] = new SpeedPostItem(stf,idx); }),
                        new STFReader.TokenProcessor("platformitem", ()=>{ TrItemTable[idx] = new PlatformItem(stf,idx); }),
                        new STFReader.TokenProcessor("soundregionitem", ()=>{ TrItemTable[idx] = new SoundRegionItem(stf,idx); }),
                        new STFReader.TokenProcessor("emptyitem", ()=>{ TrItemTable[idx] = new EmptyItem(stf,idx); }),
                        new STFReader.TokenProcessor("levelcritem", ()=>{ TrItemTable[idx] = new LevelCrItem(stf,idx); }),
                        new STFReader.TokenProcessor("sidingitem", ()=>{ TrItemTable[idx] = new SidingItem(stf,idx); }),
                        new STFReader.TokenProcessor("hazzarditem", ()=>{ TrItemTable[idx] = new HazzardItem(stf,idx); }),
                        new STFReader.TokenProcessor("pickupitem", ()=>{ TrItemTable[idx] = new PickupItem(stf,idx); }),
                    });
                }),
            });
        }
        public TrackNode[] TrackNodes;
        public TrItem[] TrItemTable;


        public int TrackNodesIndexOf(TrackNode targetTN)
        {
            for (int i = 0; i < TrackNodes.Length; ++i)
                if (TrackNodes[i] == targetTN)
                    return i;
            throw new InvalidOperationException("Program Bug: Can't Find Track Node");
        }
    }


    public class TrackNode
    {
        public TrackNode(STFReader stf, int idx, int count)
        {
            stf.MustMatch("(");
            Index = stf.ReadUInt(null);
            Debug.Assert(idx == Index, "TrackNode Index Mismatch");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("uid", ()=>{ UiD = new UiD(stf); }),
                new STFReader.TokenProcessor("trjunctionnode", ()=>{ TrJunctionNode = new TrJunctionNode(stf, idx); TrJunctionNode.TN = this; }),
                new STFReader.TokenProcessor("trvectornode", ()=>{ TrVectorNode = new TrVectorNode(stf); }),
                new STFReader.TokenProcessor("trendnode", ()=>{ TrEndNode = true; stf.SkipBlock(); }),
                new STFReader.TokenProcessor("trpins", ()=>{
                    stf.MustMatch("(");
                    Inpins = stf.ReadUInt(null);
                    Outpins = stf.ReadUInt(null);
                    TrPins = new TrPin[Inpins + Outpins];
                    for (int i = 0; i < Inpins + Outpins; ++i)
                    {
                        stf.MustMatch("TrPin");
                        TrPins[i] = new TrPin(stf, count);
                    }
                    stf.SkipRestOfBlock();
                }),
            });
            // TODO We assume there is only 2 outputs to each junction
            var expectedPins = TrJunctionNode != null ? new[] { 3, 1, 2 } : TrVectorNode != null ? new[] { 2, 1, 1 } : TrEndNode ? new[] { 1, 1, 0 } : new[] { 0, 0, 0 };
            if (TrPins.Length != expectedPins[0])
                Trace.TraceWarning("Track node {0} has unexpected number of pins; expected {1}, got {2}", Index, expectedPins[0], TrPins.Length);
            if (Inpins != expectedPins[1])
                Trace.TraceWarning("Track node {0} has unexpected number of input pins; expected {1}, got {2}", Index, expectedPins[1], TrPins.Length);
            if (Outpins != expectedPins[2])
                Trace.TraceWarning("Track node {0} has unexpected number of output pins; expected {1}, got {2}", Index, expectedPins[2], TrPins.Length);
        }
        public TrJunctionNode TrJunctionNode;
        public TrVectorNode TrVectorNode;
        
       /// <summary>
       /// True when this TrackNode has nothing else connected to it (that is, it is
       /// a buffer end or an unfinished track) and trains cannot proceed beyond here.
       /// </summary>
       public bool TrEndNode;

        public TrPin[] TrPins;
        public UiD UiD;  // only provided for TrJunctionNode and TrEndNode type of TrackNodes
        public uint Inpins;
        public uint Outpins;
		public uint Index;

#if NEW_SIGNALLING
        public ORTS.TrackCircuitXRefList TCCrossReference;  // Cross reference with TC sections
#else
        public InterlockingTrack InterlockingTrack { get; set; }
#endif
    
    }

    [DebuggerDisplay("\\{MSTS.TrPin\\} Link={Link}, Dir={Direction}")]
    public class TrPin
    {
        public TrPin(STFReader stf, int count)
        {
            stf.MustMatch("(");
            Link = stf.ReadInt(null);
            Direction = stf.ReadInt(null);
            if (Link <= 0 || Link > count)
                STFException.TraceWarning(stf, String.Format("Track node pin has invalid link {0}", Link));
            stf.SkipRestOfBlock();
        }
        public int Link;
        public int Direction;

#if NEW_SIGNALLING
        //
        // constructor for TrackCircuitSection pins
        //

        public TrPin()
        {
        }

        //
        // copy pins (from trackNode to TrackCircuitSection)
        //

        public TrPin Copy()
        {
            TrPin newPin = new TrPin();
            newPin.Direction = this.Direction;
            newPin.Link = this.Link;

            return newPin;
        }
#endif
    }

    [DebuggerDisplay("\\{MSTS.UiD\\} ID={WorldID}, TileX={TileX}, TileZ={TileZ}, X={X}, Y={Y}, Z={Z}, AX={AX}, AY={AY}, AZ={AZ}, WorldX={WorldTileX}, WorldZ={WorldTileZ}")]
    public class UiD
    {
        public int TileX, TileZ;   // location of the junction 
        public float X, Y, Z;
        public float AX, AY, AZ;

        public int WorldTileX, WorldTileZ, WorldID;  // cross reference to the entry in the world file

        public UiD(STFReader stf)
        {
            stf.MustMatch("(");
            WorldTileX = stf.ReadInt(null);
            WorldTileZ = stf.ReadInt(null);
            WorldID = stf.ReadInt(null);
            stf.ReadInt(null);
            TileX = stf.ReadInt(null);
            TileZ = stf.ReadInt(null);
            X = stf.ReadFloat(STFReader.UNITS.None, null);
            Y = stf.ReadFloat(STFReader.UNITS.None, null);
            Z = stf.ReadFloat(STFReader.UNITS.None, null);
            AX = stf.ReadFloat(STFReader.UNITS.None, null);
            AY = stf.ReadFloat(STFReader.UNITS.None, null);
            AZ = stf.ReadFloat(STFReader.UNITS.None, null);
            stf.SkipRestOfBlock();
        }

    }

    [DebuggerDisplay("\\{MSTS.TrJunctionNode\\} SelectedRoute={SelectedRoute}, ShapeIndex={ShapeIndex}")]
    public class TrJunctionNode
    {
        public int SelectedRoute;
        
		public TrackNode TN;

        public int Idx { get; private set; }

        public TrJunctionNode(STFReader stf, int idx)
        {
            Idx = idx;
            stf.MustMatch("(");
            stf.ReadString();
            ShapeIndex = stf.ReadUInt(null);
            stf.SkipRestOfBlock();
        }
        public uint ShapeIndex;

		public double angle = -1;
        public bool AngleComputed; //the angle has been set through section file

		public double GetAngle(TSectionDatFile TSectionDat) //get the angle from sections
		{
			if (AngleComputed == false)
			{
				AngleComputed = true;
				try //so many things can be in conflict for trackshapes, tracksections etc.
				{
					SectionIdx[] SectionIdxs = TSectionDat.TrackShapes.Get(ShapeIndex).SectionIdxs;

					foreach (SectionIdx id in SectionIdxs)
					{
						uint[] sections = id.TrackSections;

						for (int i = 0; i < sections.Length; i++)
						{
							uint sid = id.TrackSections[i];
							TrackSection section = TSectionDat.TrackSections[sid];

							if (section.SectionCurve != null)
							{
								angle = Math.Abs(section.SectionCurve.Angle);
								break;
							}
						}
					}
				}
				catch (Exception ) { } 
			}
			return angle;
		}
    }

    public class TrVectorNode
    {
        public TrVectorNode(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("trvectorsections", ()=>{
                    stf.MustMatch("(");
                    int count = stf.ReadInt(null);
                    TrVectorSections = new TrVectorSection[count];
                    for (int i = 0; i < count; ++i)
                        TrVectorSections[i] = new TrVectorSection(stf);
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("tritemrefs", ()=>{
                    stf.MustMatch("(");
                    noItemRefs = stf.ReadInt(null);
                    TrItemRefs = new int[noItemRefs];
                    int refidx = 0;
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("tritemref", ()=>{
                            if (refidx >= noItemRefs)
                                STFException.TraceWarning(stf, "Skipped extra TrItemRef");
                            else
                                TrItemRefs[refidx++] = stf.ReadIntBlock(null);
                        }),
                    });
                    if (refidx < noItemRefs)
                        STFException.TraceWarning(stf, (noItemRefs - refidx).ToString() + " missing TrItemRef(s)");
                }),
            });
        }
        public TrVectorSection[] TrVectorSections;
        public int[] TrItemRefs;
        public int noItemRefs =0;

        public int TrVectorSectionsIndexOf(TrVectorSection targetTVS)
        {
            for (int i = 0; i < TrVectorSections.Length; ++i)
                if (TrVectorSections[i] == targetTVS)
                    return i;
            throw new InvalidOperationException("Program Bug: Can't Find TVS");
        }

    }

    public class TrVectorSection
    {
        public TrVectorSection(STFReader stf)
        {
            SectionIndex = stf.ReadUInt(null);
            ShapeIndex = stf.ReadUInt(null);
            WFNameX = stf.ReadInt(null);// worldfilenamex
            WFNameZ = stf.ReadInt(null);// worldfilenamez
            WorldFileUiD = stf.ReadUInt(null); // UID in worldfile
            flag1 = stf.ReadInt(null); // 0
            flag2 = stf.ReadInt(null); // 1
            stf.ReadString(); // 00 
            TileX = stf.ReadInt(null);
            TileZ = stf.ReadInt(null);
            X = stf.ReadFloat(STFReader.UNITS.None, null);
            Y = stf.ReadFloat(STFReader.UNITS.None, null);
            Z = stf.ReadFloat(STFReader.UNITS.None, null);
            AX = stf.ReadFloat(STFReader.UNITS.None, null);
            AY = stf.ReadFloat(STFReader.UNITS.None, null);
            AZ = stf.ReadFloat(STFReader.UNITS.None, null);
        }
        public int flag1;   // usually 0, - may point to the connecting pin entry in a junction
        public int flag2;  // usually 1, but set to 0 when curve track is flipped around
        // I have also seen 2's in both locations
        public uint SectionIndex;
        public uint ShapeIndex;
        public int TileX;
        public int TileZ;
        public float X, Y, Z;
        public float AX, AY, AZ;
        public uint WorldFileUiD;
        public int WFNameX, WFNameZ;
        public float StartElev, EndElev, MaxElev;
        public override string ToString()
        {
            return String.Format("{{TileX:{0} TileZ:{1} X:{2} Y:{3} Z:{4} UiD:{5} Section:{6} Shape:{7}}}", WFNameX, WFNameZ, X, Y, Z, WorldFileUiD, SectionIndex, ShapeIndex);
        }
    }


    public class TrItem
    {
        public string ItemName;   // Used for the label shown by F6
        public enum trItemType
        {
            trEMPTY,
            trCROSSOVER,
            trSIGNAL,
            trSPEEDPOST,
            trPLATFORM,
            trSOUNDREGION,
            trXING,
            trSIDING,
            trHAZZARD,
            trPICKUP,
			trCarSpawner // added for road traffic spawner, used in the future
        }
        public trItemType ItemType = trItemType.trEMPTY;
        public uint TrItemId;
        public int TileX;
        public int TileZ;
        public float X, Y, Z;   //  Location within world tile. Track node not shape
        public int TilePX;
        public int TilePZ;      // Appears to be copy of SData but X,Z only.
        public float PX, PZ;   
        public float SData1;
        public string SData2;
        
        protected void TrItemID(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            TrItemId = stf.ReadUInt(null);
            Debug.Assert(idx == TrItemId, "Index Mismatch");
            stf.SkipRestOfBlock();
        }
        protected void TrItemRData(STFReader stf)
        {
            stf.MustMatch("(");
            X = stf.ReadFloat(STFReader.UNITS.None, null);
            Y = stf.ReadFloat(STFReader.UNITS.None, null);
            Z = stf.ReadFloat(STFReader.UNITS.None, null);
            TileX = stf.ReadInt(null);
            TileZ = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }

        protected void TrItemPData(STFReader stf)
        {
            stf.MustMatch("(");
            PX = stf.ReadFloat(STFReader.UNITS.None, null);
            PZ = stf.ReadFloat(STFReader.UNITS.None, null);
            TilePX = stf.ReadInt(null);
            TilePZ = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }

        protected void TrItemSData(STFReader stf)
        {
            stf.MustMatch("(");
            SData1 = stf.ReadFloat(STFReader.UNITS.None, null);
            SData2 = stf.ReadString();
            stf.SkipRestOfBlock();
        }
    } // TrItem

    public class CrossoverItem : TrItem
    {
#if !NEW_SIGNALLING
        uint TrackNode, CID1;
#else
        public uint TrackNode, CID1;
#endif
        public CrossoverItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trCROSSOVER;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("crossovertritemdata", ()=>{
                    stf.MustMatch("(");
                    TrackNode = stf.ReadUInt(null);
                    CID1 = stf.ReadUInt(null);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    public class SignalItem : TrItem
    {
        public struct strTrSignalDir
        {
            public uint TrackNode;                  // Index to the junction track node
            public uint sd1, linkLRPath, sd3;              // Used with junction signals (appears to be either 1 or 0
        }

        public string Flags1;                 // Set to  00000001 if junction link set 
        public uint Direction;                // 0 or 1 depending on which way signal is facing
        public int sigObj = -1;               // index to Sigal Object Table
        public float SigData1;
        public string SignalType;
        public uint noSigDirs=0;              // Number of junction links
        public strTrSignalDir[] TrSignalDirs;
        
        public int revDir
        {
            get {return Direction==0?1:0;}
        }

        public SignalItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trSIGNAL;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("trsignaltype", ()=>{
                    stf.MustMatch("(");
                    Flags1 = stf.ReadString();
                    Direction = stf.ReadUInt(null);
                    SigData1 = stf.ReadFloat(STFReader.UNITS.None, null);
                    SignalType = stf.ReadString().ToLowerInvariant();
                    // To do get index to Sigtypes table corresponding to this sigmal
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("trsignaldirs", ()=>{
                    stf.MustMatch("(");
                    noSigDirs = stf.ReadUInt(null);
                    TrSignalDirs = new strTrSignalDir[noSigDirs];
                    int sigidx = 0;
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("trsignaldir", ()=>{
                            if (sigidx >= noSigDirs)
                                STFException.TraceWarning(stf, "Skipped extra TrSignalDirs");
                            else
                            {
                                TrSignalDirs[sigidx]=new strTrSignalDir();
                                stf.MustMatch("(");
                                TrSignalDirs[sigidx].TrackNode = stf.ReadUInt(null);
                                TrSignalDirs[sigidx].sd1 = stf.ReadUInt(null);
                                TrSignalDirs[sigidx].linkLRPath = stf.ReadUInt(null);
                                TrSignalDirs[sigidx].sd3 = stf.ReadUInt(null);
                                stf.SkipRestOfBlock();
                                sigidx++;
                            }
                        }),
                    });
                    if (sigidx < noSigDirs)
                        STFException.TraceWarning(stf, (noSigDirs - sigidx).ToString() + " missing TrSignalDirs(s)");
                }),
            });
        }
    }

    public class SpeedPostItem : TrItem
    {
        public uint Flags;
        public bool IsMilePost; //true to be milepost
        public bool IsWarning; //speed warning
        public bool IsLimit; //speed limit
		public bool IsResume= false; // speed resume sign (has no speed defined!)
        public bool IsPassenger; //is passender speed limit
        public bool IsFreight; //is freight speed limit
        public bool IsMPH;//is the digit in MPH or KPH
        public bool ShowNumber; //show numbers instead of KPH, like 5 means 50KMH
        public bool ShowDot; //if ShowNumber is true and this is set, will show 1.5 as for 15KMH
        public float SpeedInd;      // Or distance if mile post.
	public int sigObj = -1;	    // index to Signal Object Table
	public float Angle;         // speedpost (normalized) angle
	public int Direction;       // derived direction relative to track
	public int DisplayNumber;   // number to be displayed if ShowNumber is true

        public int revDir
        {
            get {return Direction==0?1:0;}
        }

        public SpeedPostItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trSPEEDPOST;
            stf.MustMatch("(");
			stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("speedposttritemdata", ()=>{
                    stf.MustMatch("(");
                    Flags = stf.ReadUInt(null);
					if ((Flags & 1) != 0) IsWarning = true;
					if ((Flags & (1 << 1)) != 0) IsLimit = true;
					if (!IsWarning && !IsLimit) {
						IsMilePost = true;
					}
					else {
						if (IsWarning && IsLimit)
						{
							IsWarning = false;
							IsResume = true;
						}

						if ((Flags & (1 << 5)) != 0) IsPassenger = true;
						if ((Flags & (1 << 6)) != 0) IsFreight = true;
						if ((Flags & (1 << 7)) != 0) IsFreight = IsPassenger = true;
						if ((Flags & (1 << 8)) != 0) IsMPH = true;
						if ((Flags & (1 << 4)) != 0) {
							ShowNumber = true;
							if ((Flags & (1 << 9)) != 0) ShowDot = true;
						}
					}

                    //  The number of parameters depends on the flags seeting
                    //  To do: Check flags seetings and parse accordingly.
		    if (!IsResume)
		    {
                //SpeedInd = stf.ReadFloat(STFReader.UNITS.None, null);
                if (IsMilePost && ((Flags & (1 << 9)) == 0)) SpeedInd = (float)Math.Truncate(stf.ReadDouble(null));
                else SpeedInd = stf.ReadFloat(STFReader.UNITS.None, null);
		    }
    		if (ShowNumber)
		    {
			    DisplayNumber = stf.ReadInt(null);
		    }
                    
			Angle = stf.ReadFloat(STFReader.UNITS.None, null);
		    MSTSMath.M.NormalizeRadians(ref Angle);

                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    public class SoundRegionItem : TrItem
    {
        public uint SRData1, SRData2;
        public float SRData3;
        public SoundRegionItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trSOUNDREGION;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("tritemsrdata", ()=>{
                    stf.MustMatch("(");
                    SRData1 = stf.ReadUInt(null);
                    SRData2 = stf.ReadUInt(null);
                    SRData3 = stf.ReadFloat(STFReader.UNITS.None, null);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    public class EmptyItem : TrItem
    {
        public EmptyItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trEMPTY;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
            });
        }
    }

    public class LevelCrItem : TrItem
    {
        public LevelCrItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trXING;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),
            });
        }
    }

    public class SidingItem : TrItem
    {
        public string Flags1;
        public uint Flags2;

        public SidingItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trSIDING;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("sidingname", ()=>{ ItemName = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("sidingtritemdata", ()=> {
                    stf.MustMatch("(");
                    Flags1 = stf.ReadString();
                    Flags2 = stf.ReadUInt(null);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }
    
    public class PlatformItem : TrItem {
        public string Station;
        public string Flags1;
        public uint PlatformMinWaitingTime, PlatformNumPassengersWaiting;
        public uint LinkedPlatformItemId;

        public PlatformItem(STFReader stf, int idx) {
        ItemType = trItemType.trPLATFORM;
        stf.MustMatch("(");
        stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("platformname", ()=>{ ItemName = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("station", ()=>{ Station = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("platformminwaitingtime", ()=>{ PlatformMinWaitingTime = stf.ReadUIntBlock(null); }),
                new STFReader.TokenProcessor("platformnumpassengerswaiting", ()=>{ PlatformNumPassengersWaiting = stf.ReadUIntBlock(null); }),
                new STFReader.TokenProcessor("platformtritemdata", ()=>{
                    stf.MustMatch("(");
                    Flags1 = stf.ReadString();
                    LinkedPlatformItemId = stf.ReadUInt(null);
                    stf.SkipRestOfBlock();
                }),
            });
        }

#if NEW_SIGNALLING
        // constructor to create Platform Item out of Siding Item

        public PlatformItem(SidingItem thisSiding)
        {
            TrItemId = thisSiding.TrItemId;
            SData1 = thisSiding.SData1;
            SData2 = thisSiding.SData2;
            ItemName = thisSiding.ItemName;
            Flags1 = thisSiding.Flags1;
            LinkedPlatformItemId = thisSiding.Flags2;
            Station = String.Copy(ItemName);
        }
#endif
    }

    public class HazzardItem : TrItem
    {
        public HazzardItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trHAZZARD;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

            });
        }
    }

    public class PickupItem : TrItem
    {
        public PickupItem(STFReader stf, int idx)
        {
            ItemType = trItemType.trPICKUP;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ TrItemID(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

            });
        }
    }

}
