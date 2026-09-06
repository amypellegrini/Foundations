using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Play-mode regression scenario against the reconciled upstream rules.</summary>
public static class ReconciliationVerification
{
    static GameController game; static Unit founder,hunter; static HexGrid grid; static City city;
    static int stage,turnBefore; static double deadline; static Vector2Int oldSight;
    static readonly List<string> passed=new List<string>();
    public static string Begin()
    {
        Require(EditorApplication.isPlaying,"Play Mode required");passed.Clear();
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();grid=game.grid;
        founder=game.Units.First(u=>u.canFoundCity);hunter=game.Units.First(u=>u.unitName=="Hunter");
        Require(grid.columns==512&&grid.rows==256&&grid.wrap,"World dimensions/topology");
        Require(grid.IsLand(founder.col,founder.row)&&grid.IsLand(hunter.col,hunter.row)&&grid.Distance(founder.col,founder.row,hunter.col,hunter.row)==1,"Adjacent land spawn");
        Require(game.SelectedUnit==founder&&founder.MovesRemaining==2&&hunter.sightRadius==3,"Initial selection/role stats");
        int water=0;for(int c=0;c<grid.columns;c++)for(int r=0;r<grid.rows;r++)if(!grid.IsLand(c,r))water++;
        Require(water>0&&water<grid.columns*grid.rows,"Generated land and ocean");
        var sea=FindTile((c,r)=>!grid.IsLand(c,r));Require(grid.FindPath(founder.Coord,sea).Count==0,"Water is impassable");
        var pos=grid.HexCenter(0,100)-Vector3.right*.05f;
        Require(grid.TryWorldToHex(pos,out int cc,out int rr)&&cc==0&&rr==100,"Seam picking");
        var originalA=grid.MapData[0,100];var originalB=grid.MapData[511,100];
        grid.MapData[0,100]=TerrainType.Land;grid.MapData[511,100]=TerrainType.Land;
        Require(grid.FindPath(new Vector2Int(0,100),new Vector2Int(511,100)).Count==1,"Shortest path wraps east-west");
        grid.MapData[0,100]=originalA;grid.MapData[511,100]=originalB;
        passed.Add("512x256 generation, adjacent land spawn, water blocking, seam picking/pathfinding");
        var keys=InputSystem.AddDevice<Keyboard>();
        try
        {
            var method=typeof(CameraPan).GetMethod("ReadInput",BindingFlags.Static|BindingFlags.NonPublic);
            var keyList=new[]{Key.UpArrow,Key.DownArrow,Key.LeftArrow,Key.RightArrow};var expected=new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right};
            for(int i=0;i<4;i++){InputSystem.QueueStateEvent(keys,new KeyboardState(keyList[i]));InputSystem.Update();Require((Vector2)method.Invoke(null,new object[]{keys})==expected[i],"Arrow input");}
        }
        finally{InputSystem.RemoveDevice(keys);}
        game.CycleFocus();Require(game.SelectedUnit==hunter,"Tab focus");game.CycleFocus();Require(game.SelectedUnit==founder,"Tab wrap");
        Require(game.CanFoundCity(founder)&&!game.CanFoundCity(hunter),"Only Founder can found");
        game.OpenFoundCityDialog();Require(game.IsModalOpen,"Founding modal");game.Step();Require(game.Turn==1&&founder.MovesRemaining==2,"Modal input capture");game.CancelFoundCityDialog();Require(!game.IsModalOpen&&game.Cities.Count==0,"Cancel preserves Founder");
        passed.Add("Arrow keys, Tab cycle, initial status, Founder eligibility, dialog cancel and input blocking");
        // Queue routes longer than a single turn; both routes must continue one at a time.
        founder.SetPath(LongRoute(founder.Coord));hunter.SetPath(LongRoute(hunter.Coord));
        game.Select(founder);game.Step();Require(founder.IsMoving&&!hunter.IsMoving,"First unit action");
        game.Step();Require(!hunter.IsMoving&&game.Turn==1,"Animation blocks action");
        stage=0;deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        return "Reconciliation regression scenario started.";
    }
    static List<Vector2Int> LongRoute(Vector2Int start)
    {
        for(int x=-5;x<=5;x++)for(int y=-5;y<=5;y++)
        {var path=grid.FindPath(start,new Vector2Int(grid.WrapColumn(start.x+x),start.y+y));if(path.Count>=4&&path.Count<=7)return path;}
        throw new Exception("No connected multi-turn route near spawn");
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Scenario timed out at stage "+stage);
            Require(!(founder!=null&&founder.IsMoving&&hunter.IsMoving),"Sequential unit animation");
            if(game.IsBusy)return;
            if(stage==0)
            {
                Require(game.Turn==1&&founder.HasActed&&game.SelectedUnit==hunter,"Auto focus after first action");
                game.Step();stage=1;return;
            }
            if(stage==1)
            {
                Require(game.Turn==1&&hunter.HasActed,"No automatic turn rollover");
                game.Select(founder);game.Step();Require(game.Turn==2,"Explicit rollover");stage=2;return;
            }
            if(stage==2)
            {
                passed.Add("Two-unit action cycle, explicit end-turn confirmation, sequential queued auto-walks");
                founder.SetPath(null);hunter.SetPath(null);founder.RefillMoves();hunter.RefillMoves();
                // Rejoin the two units before founding, so the Hunter supports the new city.
                hunter.col=founder.col;hunter.row=founder.row;hunter.SnapToHex();game.Select(founder);
                game.OpenFoundCityDialog();typeof(GameController).GetField("foundCityName",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,"New Haven");
                game.ConfirmFoundCity();Require(game.Cities.Count==1&&game.Units.Count==1&&game.SelectedUnit==hunter,"Founder consumption and handoff");
                city=game.Cities[0];Require(city.cityName=="New Haven"&&city.population==100,"Named city starts at 100");
                Require(city.GetComponent<Collider>()!=null&&city.transform.Find("Territory")!=null,"City selection and territory visuals");
                Require(!game.CanFoundCity(hunter),"Hunter cannot found");stage=3;return;
            }
            if(stage==3)
            {
                Require(founder==null,"Founder actually destroyed");
                for(int c=city.col-3;c<=city.col+3;c++)for(int r=city.row-3;r<=city.row+3;r++)
                {int wc=grid.WrapColumn(c);if(grid.InBounds(wc,r)&&grid.Distance(city.col,city.row,wc,r)<=3)Require(game.fogOfWar.GetVisibility(wc,r)==TileVisibility.Visible,"City territory vision");}
                NextTurn();Require(city.growthProgress==1&&city.IsAccruing,"Growth starts");
                NextTurn();Require(city.growthProgress==2,"Growth progress");
                NextTurn();Require(city.population==101&&city.growthProgress==0,"One population per three turns");
                NextTurn();Require(city.growthProgress==1,"Next growth cycle");
                passed.Add("Named city, Founder consumption, 100 population, radius-three permanent vision, growth every three turns");
                var away=FindTile((c,r)=>grid.IsLand(c,r)&&grid.Distance(city.col,city.row,c,r)>12);
                hunter.col=away.x;hunter.row=away.y;hunter.SnapToHex();oldSight=away;stage=4;return;
            }
            if(stage==4)
            {
                Require(game.fogOfWar.GetVisibility(oldSight.x,oldSight.y)==TileVisibility.Visible,"Hunter sight away from city");
                NextTurn();Require(city.growthProgress==0&&!city.IsAccruing&&city.population==101,"Leaving resets growth");
                hunter.col=city.col;hunter.row=city.row;hunter.SnapToHex();stage=5;return;
            }
            if(stage==5)
            {
                Require(game.fogOfWar.GetVisibility(oldSight.x,oldSight.y)==TileVisibility.Explored,"Visible becomes explored");
                for(int i=0;i<9;i++)NextTurn();Require(game.fogOfWar.GetVisibility(oldSight.x,oldSight.y)==TileVisibility.Explored,"Memory persists for nine ticks");
                NextTurn();Require(game.fogOfWar.GetVisibility(oldSight.x,oldSight.y)==TileVisibility.Unknown,"Memory expires after ten ticks");
                Require(game.fogOfWar.GetVisibility(city.col,city.row)==TileVisibility.Visible,"City vision never decays");
                passed.Add("Growth reset/re-entry, visible/explored/unknown transitions, ten-turn decay and permanent city vision");
                File.WriteAllLines(".tw-ai/reconciliation-verification.txt",new[]{"PASS"}.Concat(passed));
                Debug.Log("Reconciliation verification PASS");EditorApplication.update-=Tick;
            }
        }
        catch(Exception e){File.WriteAllText(".tw-ai/reconciliation-verification.txt","FAIL stage "+stage+": "+e);Debug.LogException(e);EditorApplication.update-=Tick;}
    }
    static void NextTurn()
    {
        int before=game.Turn;game.Step();if(game.Turn==before)game.Step();Require(game.Turn==before+1,"One explicit turn advance");
    }
    static Vector2Int FindTile(Func<int,int,bool> predicate)
    {for(int c=0;c<grid.columns;c++)for(int r=0;r<grid.rows;r++)if(predicate(c,r))return new Vector2Int(c,r);throw new Exception("No matching tile");}
    static void Require(bool yes,string message){if(!yes)throw new Exception(message);}
    public static string ShowCityPanel()
    {
        var current=UnityEngine.Object.FindAnyObjectByType<GameController>();
        Require(current.Cities.Count>0,"Run the regression scenario first");
        typeof(GameController).GetField("selectedCity",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(current,current.Cities[0]);
        Require(current.IsModalOpen,"City status popup");return "City status panel open for visual verification.";
    }
}
