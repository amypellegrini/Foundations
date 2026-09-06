using UnityEngine;

/// <summary>3D presentation of the upstream per-unit turn cycle, city actions, and status.</summary>
public class ExpeditionHUD : MonoBehaviour
{
    public GameController game;
    GUIStyle title,small,body,button,caption;
    public static float Scale => Mathf.Max(.25f,Mathf.Min(Screen.height/900f,Screen.width/1100f));
    public static bool ContainsPointer(Vector2 p)
    {
        float s=Scale,y=Screen.height-p.y;
        return y<100*s || y>Screen.height-150*s || (p.x<270*s && y>Screen.height-320*s);
    }
    void OnGUI()
    {
        if(game.IsModalOpen)return;
        float s=Scale;GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1));float w=Screen.width/s,h=Screen.height/s;
        title ??= new GUIStyle(GUI.skin.label){fontSize=29,fontStyle=FontStyle.Bold,normal={textColor=new Color(.91f,.84f,.66f)}};
        small ??= new GUIStyle(GUI.skin.label){fontSize=12,normal={textColor=new Color(.72f,.80f,.77f)}};
        body ??= new GUIStyle(GUI.skin.label){fontSize=17,normal={textColor=new Color(.94f,.93f,.85f)}};
        button ??= new GUIStyle(GUI.skin.button){fontSize=16,fontStyle=FontStyle.Bold};
        caption ??= new GUIStyle(body){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
        Panel(new Rect(0,0,w,98));Panel(new Rect(0,h-148,w,148));
        GUI.Label(new Rect(28,12,500,40),"F O U N D A T I O N S",title);
        GUI.Label(new Rect(30,58,750,25),"THE FIRST SETTLEMENT    /    3D EDITION    /    512 x 256 WORLD",small);
        GUI.Label(new Rect(w-185,22,180,35),$"TURN  {game.Turn:00}",title);
        float x=28;
        foreach(var unit in game.Units)
        {
            if(unit==null)continue;
            string state=unit.HasActed?"DONE":$"{unit.MovesRemaining}/{unit.movesPerTurn} MOVES";
            GUI.Label(new Rect(x,h-132,290,26),$"{(unit==game.SelectedUnit?"> ":"")}{unit.unitName}   {state}",body);
            GUI.Label(new Rect(x,h-102,290,24),$"Route: {unit.Path.Count} hexes    Sight: {unit.sightRadius}",small);x+=290;
        }
        GUI.Label(new Rect(28,h-70,w-290,24),$"Cities: {game.Cities.Count}    Tab: cycle units    B: found city    Space: move / skip / next turn",small);
        GUI.Label(new Rect(28,h-39,w-290,24),"Arrows / WASD: pan   |   Left-drag: pan   |   Right-drag: orbit   |   Scroll: zoom   |   Home: recenter",small);
        GUI.enabled=!game.IsBusy;
        var u=game.SelectedUnit;
        string action=game.IsBusy?"MOVING...":game.AllSpent?"NEXT TURN / SPACE":u!=null&&!u.HasActed?(u.Path.Count>0?"MOVE / SPACE":"SKIP / SPACE"):"CONTINUE / SPACE";
        if(GUI.Button(new Rect(w-245,h-66,216,43),action,button))game.Step();
        if(game.CanFoundCity(u)&&GUI.Button(new Rect(w-245,h-118,216,43),"FOUND CITY / B",button))game.OpenFoundCityDialog();
        GUI.enabled=true;
        var minimap=GetComponent<Minimap>();if(minimap!=null){minimap.size=new Vector2(240,140)*s;minimap.margin=16*s;minimap.bottomOffset=155*s;}
        GUI.Label(new Rect(18,h-338,240,23),"DISCOVERED WORLD",small);
        GUI.matrix=Matrix4x4.identity;
        if(u!=null)DrawCaption(u.transform.position+Vector3.up*.12f,u.unitName,24);
        foreach(var city in game.Cities)if(city!=null)
            DrawCaption(city.transform.position+Vector3.up*1.9f,city.cityName+(city.IsAccruing?$"   +1 ({city.growthProgress}/{City.TurnsPerGrowth})":""));
    }
    void DrawCaption(Vector3 position,string text,float offset=0)
    {
        Vector3 p=game.cam.WorldToScreenPoint(position);if(p.z<=0)return;
        float s=Scale;var rect=new Rect(p.x-130*s,Screen.height-p.y-15*s+offset*s,260*s,30*s);
        GUI.color=new Color(.035f,.07f,.085f,.85f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;
        caption.fontSize=Mathf.RoundToInt(16*s);GUI.Label(rect,text,caption);
    }
    static void Panel(Rect rect)
    {
        GUI.color=new Color(.045f,.095f,.105f,.97f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=new Color(.66f,.53f,.30f);GUI.DrawTexture(new Rect(rect.x,rect.y==0?rect.yMax-2:rect.y,rect.width,2),Texture2D.whiteTexture);GUI.color=Color.white;
    }
}
