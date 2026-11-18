using System;
using System.Collections.Generic;
using System.Reflection;
using MeshFrame.Core;
namespace MeshFrame.Tekla;

public static class TeklaViewReader
{
    public static List<ViewOrientation> Read(TeklaApi a,object model) {
        var result=new List<ViewOrientation>();
        object handler=a.Call(model,"GetWorkPlaneHandler"),saved=a.Call(handler,"GetCurrentTransformationPlane");
        try {
            a.Require(handler,"SetCurrentTransformationPlane","Đặt hệ toàn cục",a.New(a.ModelType("TransformationPlane")));
            object views=a.ModelType("UI.ViewHandler").GetMethod("GetVisibleViews",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
            while((bool)a.Call(views,"MoveNext")) {
                object view=a.Get(views,"Current"),cs=a.Get(view,"DisplayCoordinateSystem");
                result.Add(new ViewOrientation{Right=Vector(a,a.Get(cs,"AxisX")),Up=Vector(a,a.Get(cs,"AxisY")),Name=Convert.ToString(a.Get(view,"Name"))});
            }
        } finally { a.Call(handler,"SetCurrentTransformationPlane",saved); }
        return result;
    }
    private static V3 Vector(TeklaApi a,object v)=>new V3(Convert.ToDouble(a.Get(v,"X")),Convert.ToDouble(a.Get(v,"Y")),Convert.ToDouble(a.Get(v,"Z")));
}
