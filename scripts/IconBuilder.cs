using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
class IconBuilder {
    static GraphicsPath Rounded(float x,float y,float w,float h,float r) { var p=new GraphicsPath();p.AddArc(x,y,r*2,r*2,180,90);p.AddArc(x+w-r*2,y,r*2,r*2,270,90);p.AddArc(x+w-r*2,y+h-r*2,r*2,r*2,0,90);p.AddArc(x,y+h-r*2,r*2,r*2,90,90);p.CloseFigure();return p; }
    static Bitmap Draw(int size) {
        var b=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(b)) {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.ScaleTransform(size/256f,size/256f);
            using(var shape=Rounded(4,4,248,248,37))using(var bg=new SolidBrush(Color.FromArgb(23,35,49)))g.FillPath(bg,shape);
            g.TranslateTransform(17,10);g.ScaleTransform(.88f,.88f);
            using(var left=new GraphicsPath()){left.AddPolygon(new[]{new PointF(26,79),new PointF(151,19),new PointF(151,72),new PointF(79,107),new PointF(79,136),new PointF(102,125),new PointF(102,185),new PointF(26,222)});using(var fill=new SolidBrush(Color.FromArgb(237,242,240)))g.FillPath(fill,left);}
            using(var right=new GraphicsPath(FillMode.Alternate)){
                right.AddPolygon(new[]{new PointF(122,119),new PointF(151,105),new PointF(151,85),new PointF(220,52),new PointF(220,202),new PointF(96,244),new PointF(96,199),new PointF(122,185)});
                foreach(float y in new[]{174f,192f})using(var slit=new GraphicsPath())using(var pen=new Pen(Color.Black,7)){pen.StartCap=pen.EndCap=LineCap.Round;slit.AddLine(167,y+15,202,y);slit.Widen(pen);right.AddPath(slit,false);}
                using(var fill=new SolidBrush(Color.FromArgb(94,158,160)))g.FillPath(fill,right);
            }
        }return b;
    }
    static void Main(string[] args) {
        var sizes=new[]{16,20,24,32,40,48,64,128,256};var entries=new List<byte[]>();
        foreach(var size in sizes)using(var b=Draw(size))using(var stream=new MemoryStream()){b.Save(stream,ImageFormat.Png);entries.Add(stream.ToArray());}
        using(var output=new BinaryWriter(File.Create(args[0]))){output.Write((ushort)0);output.Write((ushort)1);output.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){output.Write((byte)(sizes[i]==256?0:sizes[i]));output.Write((byte)(sizes[i]==256?0:sizes[i]));output.Write((byte)0);output.Write((byte)0);output.Write((ushort)1);output.Write((ushort)32);output.Write(entries[i].Length);output.Write(offset);offset+=entries[i].Length;}foreach(var bytes in entries)output.Write(bytes);}
        if(args.Length>1)using(var b=Draw(256))b.Save(args[1],ImageFormat.Png);
    }
}
