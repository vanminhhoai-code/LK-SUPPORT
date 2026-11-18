using System;
using System.Reflection;
using System.Windows.Forms;
using MeshFrame.Core;
using MeshFrame.UI;
class UndoTests {
 static int count;
 static object Get(object d,string n)=>d.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(d);
 static object Call(object d,string n,params object[] args)=>d.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(d,args);
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static V3[] Rect()=>new[]{new V3(0,0,0),new V3(3640,0,0),new V3(3640,500,0),new V3(0,500,0)};
 static void Undo(MeshFrameDialog d){object[] args={new Message(),Keys.Control|Keys.Z};Check((bool)Call(d,"ProcessCmdKey",args),"CtrlZ not handled");Application.DoEvents();}
 static void Equal(V3[] a,V3[] b){Check(a.Length==b.Length,"wrong point count");for(int i=0;i<a.Length;i++)Check((a[i]-b[i]).Length<1e-8,"wrong point "+i);}
 static void Run(string name,Action<MeshFrameDialog> test){using(var d=new MeshFrameDialog()){d.Settings=new MeshFrameSettings{AnchorPoint=4,StiffBaseEdge=1,StiffMode=StiffMode.Custom,StiffCells="1000 1000 1000 640"};d.Points=Rect();d.Show();Application.DoEvents();test(d);d.Close();count++;Console.WriteLine("PASS "+name);}}
 [STAThread]static int Main(){Application.EnableVisualStyles();try{
 Run("delete point CtrlZ restores point order, coordinates, anchor, edit target",d=>{d.EditingId="same-frame";var g=(DataGridView)Get(d,"_grid");g.CurrentCell=g.Rows[1].Cells[1];((Button)Get(d,"_btnDeletePoint")).PerformClick();Check(d.Points.Length==3&&d.Settings.AnchorPoint==3,"delete failed");Check(((Button)Get(d,"_btnUndo")).Enabled,"undo button disabled");Undo(d);Equal(d.Points,Rect());Check(d.Settings.AnchorPoint==4&&d.EditingId=="same-frame","state lost");});
 Run("delete anchor P4 then undo",d=>{var g=(DataGridView)Get(d,"_grid");g.CurrentCell=g.Rows[3].Cells[1];((Button)Get(d,"_btnDeletePoint")).PerformClick();Check(d.Settings.AnchorPoint==3,"invalid anchor after delete");Undo(d);Equal(d.Points,Rect());Check(d.Settings.AnchorPoint==4,"anchor not restored");});
 Run("edge dimension CtrlZ restores whole shape",d=>{Call(d,"OnDimEdited",new DimItem{Kind=DimKind.Edge,Index=0},4000d);Check(Math.Abs((d.Points[1]-d.Points[0]).Length-4000)<.001,"edge not edited");Undo(d);Equal(d.Points,Rect());});
 Run("cell dimension CtrlZ restores cells without resizing frame",d=>{Call(d,"OnDimEdited",new DimItem{Kind=DimKind.Cell,Index=1},1500d);Check(d.Settings.StiffCells!="1000 1000 1000 640","cell not edited");Undo(d);Equal(d.Points,Rect());Check(d.Settings.StiffCells=="1000 1000 1000 640","cells not restored");});
 Run("coordinate committed then CtrlZ",d=>{var g=(DataGridView)Get(d,"_grid");g.CurrentCell=g.Rows[0].Cells[1];g.BeginEdit(true);((TextBox)g.EditingControl).Text="-100";g.CurrentCell=g.Rows[1].Cells[1];Check(d.Points[0].X==-100,"coordinate not committed");Undo(d);Equal(d.Points,Rect());});
 Run("CtrlZ while coordinate edit still active",d=>{var g=(DataGridView)Get(d,"_grid");g.CurrentCell=g.Rows[0].Cells[1];g.BeginEdit(true);((TextBox)g.EditingControl).Text="-100";Undo(d);Equal(d.Points,Rect());});
 Run("multiple edits undo in reverse order",d=>{Call(d,"OnDimEdited",new DimItem{Kind=DimKind.Edge,Index=0},4000d);V3[] resized=(V3[])d.Points.Clone();var g=(DataGridView)Get(d,"_grid");g.CurrentCell=g.Rows[1].Cells[1];((Button)Get(d,"_btnDeletePoint")).PerformClick();Undo(d);Equal(d.Points,resized);Undo(d);Equal(d.Points,Rect());Check(!((Button)Get(d,"_btnUndo")).Enabled,"extra history entry");});
 Run("no history CtrlZ is safe",d=>{Undo(d);Equal(d.Points,Rect());});
 Console.WriteLine("RESULT "+count+" passed 0 failed");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e.GetBaseException());return 1;}}
}
