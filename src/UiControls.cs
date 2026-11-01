using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ScreenText {
    public class RegionPicker : Form {
        readonly Bitmap desktop; readonly Rectangle bounds; readonly Color selectionColor; Point start,current; bool selecting;
        public Rectangle Selected;
        public RegionPicker(Bitmap screenshot, Rectangle virtualScreen,Color? accent=null) {
            selectionColor=accent??Color.FromArgb(133,188,174);
            desktop=screenshot; bounds=virtualScreen; FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual;
            Bounds=bounds; TopMost=true; ShowInTaskbar=false; DoubleBuffered=true; Cursor=Cursors.Cross; KeyPreview=true;
            KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape)CancelCapture();};
            MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Right){CancelCapture();return;} if(e.Button!=MouseButtons.Left)return; start=current=e.Location; selecting=true; Capture=true; Invalidate(); };
            MouseMove+=(s,e)=>{if(selecting){current=e.Location;Invalidate();}};
            MouseUp+=(s,e)=>{ if(!selecting || e.Button!=MouseButtons.Left)return; current=e.Location; var r=Selection(); if(r.Width<5 || r.Height<5){selecting=false;Invalidate();return;} Selected=new Rectangle(r.X+bounds.X,r.Y+bounds.Y,r.Width,r.Height); DialogResult=DialogResult.OK;Close(); };
        }
        void CancelCapture(){selecting=false;Capture=false;Selected=Rectangle.Empty;DialogResult=DialogResult.Cancel;Close();}
        Rectangle Selection() { return Rectangle.Intersect(new Rectangle(Math.Min(start.X,current.X),Math.Min(start.Y,current.Y),Math.Abs(current.X-start.X),Math.Abs(current.Y-start.Y)),new Rectangle(Point.Empty,bounds.Size)); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.DrawImageUnscaled(desktop,0,0);
            using(var shade=new SolidBrush(Color.FromArgb(115,15,23,42))) e.Graphics.FillRectangle(shade,ClientRectangle);
            if(selecting) { var r=Selection(); if(r.Width>0&&r.Height>0) e.Graphics.DrawImage(desktop,r,r,GraphicsUnit.Pixel); using(var pen=new Pen(selectionColor,2)) e.Graphics.DrawRectangle(pen,r); }
            var primary=Screen.PrimaryScreen.Bounds;var pill=new Rectangle(primary.X-bounds.X+(primary.Width-400)/2,primary.Y-bounds.Y+24,400,44);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var shape=UiDrawing.Rounded(pill,10))using(var fill=new SolidBrush(Color.FromArgb(245,23,31,33)))e.Graphics.FillPath(fill,shape);
            using(var font=new Font("Segoe UI",10,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,"Drag to select a region",font,new Rectangle(pill.X+18,pill.Y,260,pill.Height),Color.FromArgb(230,236,233),TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
            using(var font=new Font("Segoe UI",9))TextRenderer.DrawText(e.Graphics,"Right-click / Esc",font,new Rectangle(pill.Right-142,pill.Y,124,pill.Height),Color.FromArgb(151,168,162),TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
        }
    }

    // A managed selector: one paint path for normal, hover, focus and disabled states.
    // Avoid drawing over the native ComboBox non-client area.
    public class ThemeCheckBox:CheckBox {
        public Color AccentColor=Color.SteelBlue,BorderColor=Color.Gray,CheckColor=Color.White;bool hovered;
        public ThemeCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);AutoSize=true;Cursor=Cursors.Hand;}
        public override Size GetPreferredSize(Size proposed){var text=TextRenderer.MeasureText(Text,Font);return new Size(text.Width+38,Math.Max(34,Font.Height+14));}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovered=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=false;Invalidate();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var outside=new SolidBrush(BackColor))e.Graphics.FillRectangle(outside,ClientRectangle);float size=Math.Max(20,Font.Height+2),y=(Height-size)/2f;
            var line=Enabled&&(hovered||Focused||Checked)?AccentColor:BorderColor;using(var shape=UiDrawing.Rounded(new RectangleF(1,y,size,size),4))using(var fill=new SolidBrush(Checked?AccentColor:BackColor))using(var pen=new Pen(line,1.5f)){e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);}
            if(Checked)using(var pen=new Pen(CheckColor,2.2f)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;e.Graphics.DrawLines(pen,new[]{new PointF(1+size*.23f,y+size*.51f),new PointF(1+size*.44f,y+size*.72f),new PointF(1+size*.8f,y+size*.28f)});}
            var text=new Rectangle((int)size+13,0,Math.Max(0,Width-(int)size-13),Height);TextRenderer.DrawText(e.Graphics,Text,Font,text,Enabled?ForeColor:BorderColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,text,ForeColor,BackColor);
        }
    }
    public class ShortcutCapture:Control {
        public Color BorderColor=Color.Gray,AccentColor=Color.SteelBlue;bool hovered;
        public ShortcutCapture(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);TabStop=true;Size=new Size(300,42);Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.Text;}
        protected override bool IsInputKey(Keys keys){return (keys&Keys.KeyCode)!=Keys.Tab||(keys&Keys.Modifiers)!=0;}
        protected override void OnClick(EventArgs e){base.OnClick(e);Focus();}
        protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovered=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=false;Invalidate();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){using(var outside=new SolidBrush(BackColor))e.Graphics.FillRectangle(outside,ClientRectangle);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var shape=UiDrawing.Rounded(new RectangleF(.75f,.75f,Width-1.5f,Height-1.5f),7))using(var pen=new Pen(Focused||hovered?AccentColor:BorderColor,Focused?1.6f:1))e.Graphics.DrawPath(pen,shape);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(12,0,Width-24,Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);}
    }
    public class ThemeCombo : Control {
        public readonly List<object> Items=new List<object>();
        public ComboBoxStyle DropDownStyle{get;set;}
        public Color BorderColor=Color.Gray,ArrowColor=Color.Gray,SelectionColor=Color.Gray,DisabledForeColor=Color.Gray;public int SeparateAfterIndex=-1;
        public event EventHandler SelectedIndexChanged;
        int selectedIndex=-1;bool hovered,suppressOwnerClick;public bool OpenUpwards;ToolStripDropDown options;SelectorList optionList;
        public ThemeCombo(){SetStyle(ControlStyles.StandardDoubleClick,false);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);TabStop=true;Size=new Size(180,38);Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.ComboBox;}
        public int SelectedIndex{get{return selectedIndex;}set{if(value < -1||value>=Items.Count)throw new ArgumentOutOfRangeException("value");if(selectedIndex==value)return;selectedIndex=value;Text=value<0?"":Items[value].ToString();Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
        public bool DroppedDown{get{return options!=null&&options.Visible;}set{if(value)ShowOptions();else if(options!=null)options.Close();}}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovered=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=false;Invalidate();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);suppressOwnerClick=false;if(!Enabled)DroppedDown=false;hovered=false;Invalidate();}
        protected override void OnClick(EventArgs e){base.OnClick(e);Focus();if(suppressOwnerClick){suppressOwnerClick=false;return;}if(DroppedDown)DroppedDown=false;else ShowOptions();}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!ClientRectangle.Contains(e.Location))suppressOwnerClick=false;}
        // ToolStrip auto-dismiss happens before the selector receives the same mouse click.
        void PopupClosed(ToolStripDropDownClosedEventArgs e,Point screenCursor,MouseButtons buttons){suppressOwnerClick=e.CloseReason==ToolStripDropDownCloseReason.AppClicked&&buttons==MouseButtons.Left&&RectangleToScreen(ClientRectangle).Contains(screenCursor);hovered=false;Invalidate();}
        protected override bool IsInputKey(Keys keyData){var key=keyData&Keys.KeyCode;return key==Keys.Up||key==Keys.Down||key==Keys.Home||key==Keys.End||key==Keys.Enter||key==Keys.Space||key==Keys.F4||base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e){
            if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter||e.KeyCode==Keys.F4||(e.Alt&&e.KeyCode==Keys.Down)){suppressOwnerClick=false;if(DroppedDown)DroppedDown=false;else ShowOptions();e.Handled=e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Up||e.KeyCode==Keys.Home||e.KeyCode==Keys.End){if(Items.Count>0)SelectedIndex=e.KeyCode==Keys.Home?0:e.KeyCode==Keys.End?Items.Count-1:Math.Max(0,Math.Min(Items.Count-1,SelectedIndex+(e.KeyCode==Keys.Down?1:-1)));e.Handled=e.SuppressKeyPress=true;}base.OnKeyDown(e);
        }
        void ShowOptions(){
            if(!Enabled||Items.Count==0||DroppedDown)return;suppressOwnerClick=false;
            if(options==null){
                optionList=new SelectorList();options=new ToolStripDropDown{AutoSize=false,Padding=Padding.Empty,Margin=Padding.Empty,DropShadowEnabled=true,Renderer=new PopupRenderer()};
                var host=new ToolStripControlHost(optionList){AutoSize=false,Padding=Padding.Empty,Margin=Padding.Empty};options.Items.Add(host);
                optionList.Choose+=(index)=>{options.Close();SelectedIndex=index;Focus();};optionList.Cancel+=()=>{options.Close();Focus();};options.Closed+=(s,e)=>PopupClosed(e,Cursor.Position,Control.MouseButtons);
            }
            optionList.Font=Font;optionList.BackColor=BackColor;optionList.ForeColor=ForeColor;optionList.BorderColor=BorderColor;optionList.AccentColor=ArrowColor;optionList.SelectionColor=SelectionColor;optionList.Values=Items.Select(i=>i.ToString()).ToArray();optionList.Current=SelectedIndex;optionList.Active=SelectedIndex;optionList.SeparateAfterIndex=SeparateAfterIndex;
            int height=optionList.RowHeight*Items.Count+12;options.Size=new Size(Width,height);options.Items[0].Size=optionList.Size=new Size(Width,height);
            using(var shape=UiDrawing.Rounded(new RectangleF(0,0,Width,height),8)){var old=options.Region;options.Region=new Region(shape);if(old!=null)old.Dispose();}
            if(OpenUpwards)options.Show(this,new Point(0,-5),ToolStripDropDownDirection.AboveRight);else options.Show(this,new Point(0,Height+5));optionList.Focus();Invalidate();
        }
        public void DrawPopupPreview(Bitmap target,Point windowOrigin){if(!DroppedDown)return;using(var popup=new Bitmap(options.Width,options.Height)){options.DrawToBitmap(popup,new Rectangle(Point.Empty,options.Size));using(var g=Graphics.FromImage(target)){int x=options.Left-windowOrigin.X,y=options.Top-windowOrigin.Y;using(var shape=UiDrawing.Rounded(new RectangleF(x,y,options.Width,options.Height),8)){g.SetClip(shape);g.DrawImageUnscaled(popup,x,y);}}}}
        class PopupRenderer:ToolStripProfessionalRenderer{protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){}}
        public class SelectorList:Control {
            public string[] Values=new string[0];public int Current=-1,Active=-1,SeparateAfterIndex=-1;public Color BorderColor,AccentColor,SelectionColor;
            public event Action<int> Choose;public event Action Cancel;
            public int RowHeight{get{return Math.Max(34,Font.Height+18);}}
            public SelectorList(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);TabStop=true;AccessibleRole=AccessibleRole.List;Cursor=Cursors.Hand;}
            protected override bool IsInputKey(Keys keyData){return true;}
            protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int index=(e.Y-6)/RowHeight;if(e.Y<6||index>=Values.Length)index=-1;if(Active!=index){Active=index;Invalidate();}}
            protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Active=-1;Invalidate();}
            protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);int index=(e.Y-6)/RowHeight;if(e.Button==MouseButtons.Left&&e.Y>=6&&index>=0&&index<Values.Length&&Choose!=null)Choose(index);}
            protected override void OnKeyDown(KeyEventArgs e){
                if(e.KeyCode==Keys.Escape||e.KeyCode==Keys.Tab){if(Cancel!=null)Cancel();}
                else if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){if(Active>=0&&Active<Values.Length&&Choose!=null)Choose(Active);}
                else if(e.KeyCode==Keys.Home)Active=0;else if(e.KeyCode==Keys.End)Active=Values.Length-1;
                else if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)Active=Math.Max(0,Math.Min(Values.Length-1,(Active<0?Current:Active)+(e.KeyCode==Keys.Down?1:-1)));else{base.OnKeyDown(e);return;}
                Invalidate();e.Handled=e.SuppressKeyPress=true;
            }
            protected override void OnPaintBackground(PaintEventArgs e){}
            protected override void OnPaint(PaintEventArgs e){
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var background=new SolidBrush(BackColor))e.Graphics.FillRectangle(background,ClientRectangle);
                for(int i=0;i<Values.Length;i++){
                    if(i==SeparateAfterIndex+1&&SeparateAfterIndex>=0)using(var separator=new Pen(BorderColor))e.Graphics.DrawLine(separator,12,5+i*RowHeight,Width-12,5+i*RowHeight);var row=new Rectangle(6,6+i*RowHeight,Width-12,RowHeight);if(i==Current||i==Active)using(var path=UiDrawing.Rounded(new RectangleF(row.X,row.Y,row.Width,row.Height),5))using(var fill=new SolidBrush(SelectionColor))e.Graphics.FillPath(fill,path);
                    TextRenderer.DrawText(e.Graphics,Values[i],Font,new Rectangle(13,row.Y,Width-50,row.Height),i==Current?AccentColor:ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
                    if(i==Current){float x=Width-26,y=row.Y+row.Height/2f;using(var pen=new Pen(AccentColor,1.6f)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;e.Graphics.DrawLines(pen,new[]{new PointF(x-4,y),new PointF(x-1,y+3),new PointF(x+5,y-4)});}}
                }
                using(var outline=UiDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),8))using(var pen=new Pen(BorderColor))e.Graphics.DrawPath(pen,outline);
            }
        }
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){
            using(var outside=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(outside,ClientRectangle);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=UiDrawing.Rounded(new RectangleF(.75f,.75f,Width-1.5f,Height-1.5f),7))using(var fill=new SolidBrush(BackColor))using(var pen=new Pen(Enabled&&(Focused||hovered||DroppedDown)?ArrowColor:BorderColor,Enabled&&Focused?1.5f:1)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
            float cx=Width-18,cy=Height/2f;using(var pen=new Pen(Enabled?ArrowColor:DisabledForeColor,1.5f)){pen.StartCap=pen.EndCap=LineCap.Round;float direction=OpenUpwards?-1:1;e.Graphics.DrawLines(pen,new[]{new PointF(cx-4,cy-2*direction),new PointF(cx,cy+2*direction),new PointF(cx+4,cy-2*direction)});}
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(12,0,Math.Max(0,Width-44),Height),Enabled?ForeColor:DisabledForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
        protected override void Dispose(bool disposing){if(disposing&&options!=null){options.Dispose();options=null;}base.Dispose(disposing);}

    }
    public static class UiDrawing {
        public static GraphicsPath Rounded(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    }
    public class EditorPlaceholder:FlowLayoutPanel {
        public EditorPlaceholder(){SetStyle(ControlStyles.Selectable,true);TabStop=false;}
    }
    public class RoundedPanel:Panel {
        public Color BorderColor=Color.Gray;
        public RoundedPanel(){DoubleBuffered=true;ResizeRedraw=true;}
        protected override void OnPaintBackground(PaintEventArgs e){using(var outside=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(outside,ClientRectangle);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=UiDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),12))using(var fill=new SolidBrush(BackColor))e.Graphics.FillPath(fill,path);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=UiDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),12))using(var pen=new Pen(BorderColor))e.Graphics.DrawPath(pen,path);}
    }
    public class ThemeButton : Button {
        public Color InactiveForeColor=Color.Gray,DisabledBackColor=Color.Empty,DisabledBorderColor=Color.Gray;public bool Border=true;public string Glyph="";public Color SwatchColor=Color.Empty;bool hovered;
        public ThemeButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovered=true;Invalidate();}protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=false;Invalidate();}protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            using(var outside=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(outside,ClientRectangle);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var fill=Enabled?BackColor:DisabledBackColor.IsEmpty?BackColor:DisabledBackColor;if(hovered&&Enabled){int delta=fill.GetBrightness()>.6f?-7:9;fill=Color.FromArgb(Math.Max(0,Math.Min(255,fill.R+delta)),Math.Max(0,Math.Min(255,fill.G+delta)),Math.Max(0,Math.Min(255,fill.B+delta)));}using(var path=UiDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),7)){using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);if(Border)using(var pen=new Pen(Enabled?FlatAppearance.BorderColor:DisabledBorderColor))e.Graphics.DrawPath(pen,path);}
            var color=Enabled?ForeColor:InactiveForeColor;var rect=ClientRectangle;var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis;
            if(Glyph!=""){float x=Text.Length==0?(Width-16)/2f:14,y=(Height-16)/2f;using(var pen=new Pen(color,1.5f)){pen.LineJoin=LineJoin.Round;if(Glyph=="capture"){e.Graphics.DrawLines(pen,new[]{new PointF(x,y+5),new PointF(x,y),new PointF(x+5,y)});e.Graphics.DrawLines(pen,new[]{new PointF(x+11,y),new PointF(x+16,y),new PointF(x+16,y+5)});e.Graphics.DrawLines(pen,new[]{new PointF(x,y+11),new PointF(x,y+16),new PointF(x+5,y+16)});e.Graphics.DrawLines(pen,new[]{new PointF(x+11,y+16),new PointF(x+16,y+16),new PointF(x+16,y+11)});}else if(Glyph=="menu"){for(int offset=2;offset<=14;offset+=6)e.Graphics.DrawLine(pen,x,y+offset,x+16,y+offset);}else if(Glyph=="expand"){e.Graphics.DrawLines(pen,new[]{new PointF(x+6,y+3),new PointF(x,y+3),new PointF(x,y+16),new PointF(x+13,y+16),new PointF(x+13,y+10)});e.Graphics.DrawLines(pen,new[]{new PointF(x+9,y),new PointF(x+16,y),new PointF(x+16,y+7)});e.Graphics.DrawLine(pen,x+7,y+9,x+16,y);}else if(Glyph=="folder"){e.Graphics.DrawPolygon(pen,new[]{new PointF(x,y+3),new PointF(x+6,y+3),new PointF(x+8,y+5),new PointF(x+16,y+5),new PointF(x+16,y+15),new PointF(x,y+15)});}else{e.Graphics.DrawRectangle(pen,x+2,y+3,12,13);e.Graphics.DrawRectangle(pen,x+5,y,6,4);}}rect=new Rectangle(38,0,Math.Max(0,Width-46),Height);flags|=TextFormatFlags.Left;}else if(!SwatchColor.IsEmpty){using(var swatch=new SolidBrush(SwatchColor))e.Graphics.FillEllipse(swatch,13,Height/2f-5,10,10);rect=new Rectangle(33,0,Width-39,Height);flags|=TextFormatFlags.Left;}else if(TextAlign==ContentAlignment.MiddleLeft){rect=new Rectangle(14,0,Width-28,Height);flags|=TextFormatFlags.Left;}else flags|=TextFormatFlags.HorizontalCenter;
            if(Text.Length>0)TextRenderer.DrawText(e.Graphics,Text,Font,rect,color,flags);if(Enabled&&Focused&&ShowFocusCues)using(var pen=new Pen(color)){pen.DashStyle=DashStyle.Dot;e.Graphics.DrawRectangle(pen,4,4,Width-9,Height-9);}
        }
    }
    public class AccountChip:Button {
        public Color Ink=Color.White,Muted=Color.Gray,Accent=Color.SteelBlue,Surface=Color.DimGray,HoverSurface=Color.Gray,Attention=Color.Goldenrod;bool hovered;string state="SignedOut";
        string subtitle="";
        public string Subtitle{get{return subtitle;}set{value=value??"";if(subtitle==value)return;subtitle=value;Invalidate();}}
        public bool HasSubtitle{get{return state=="SignedIn"&&!String.IsNullOrWhiteSpace(subtitle);}}
        public string ConnectionState{get{return state;}set{if(state==value)return;state=value;Invalidate();}}
        public AccountChip(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=new Font("Segoe UI",9);Cursor=Cursors.Hand;}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hovered=true;Invalidate();}protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hovered=false;Invalidate();}protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){
            float scale=Height/(HasSubtitle?40f:32f),cy=Height/2f;using(var outside=new SolidBrush(BackColor))e.Graphics.FillRectangle(outside,ClientRectangle);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=UiDrawing.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),Height/2f-1))using(var fill=new SolidBrush(Enabled&&hovered?HoverSurface:Surface))e.Graphics.FillPath(fill,path);
            var color=Enabled?Ink:Muted;float avatarX=8*scale,avatarSize=22*scale,avatarCenter=avatarX+avatarSize/2;
            var avatarColor=!Enabled?Muted:state=="SignedIn"?Accent:state=="Reconnect"?Attention:Muted;
            using(var fill=new SolidBrush(Color.FromArgb(Enabled?26:12,avatarColor)))e.Graphics.FillEllipse(fill,avatarX,cy-avatarSize/2,avatarSize,avatarSize);
            using(var pen=new Pen(avatarColor,1.3f*scale)){pen.StartCap=pen.EndCap=LineCap.Round;e.Graphics.DrawEllipse(pen,avatarCenter-2.5f*scale,cy-6*scale,5*scale,5*scale);e.Graphics.DrawArc(pen,avatarCenter-6*scale,cy+1*scale,12*scale,9*scale,195,150);}
            var flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis;int textX=(int)(38*scale),textWidth=Math.Max(0,Width-(int)((state=="SignedIn"||state=="Reconnect"?50:70)*scale));
            if(HasSubtitle){TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(textX,(int)(3*scale),textWidth,(int)(16*scale)),color,flags);using(var detailFont=new Font(Font.FontFamily,Math.Max(7,Font.SizeInPoints-1),FontStyle.Regular))TextRenderer.DrawText(e.Graphics,subtitle,detailFont,new Rectangle(textX,(int)(19*scale),textWidth,(int)(15*scale)),Muted,flags);}
            else TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(textX,0,textWidth,Height),color,flags);
            float x=Width-22*scale,stateY=HasSubtitle?11*scale:cy;using(var pen=new Pen(!Enabled?Muted:state=="SignedIn"?Accent:state=="Reconnect"?Attention:Muted,1.5f*scale)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
                if(state=="Cancel"){e.Graphics.DrawLine(pen,x-2*scale,stateY-3*scale,x+4*scale,stateY+3*scale);e.Graphics.DrawLine(pen,x-2*scale,stateY+3*scale,x+4*scale,stateY-3*scale);}
                else if(state=="Working")e.Graphics.DrawArc(pen,x-4*scale,stateY-5*scale,10*scale,10*scale,30,280);
                else if(state=="SignedOut")e.Graphics.DrawLines(pen,new[]{new PointF(x,stateY-3*scale),new PointF(x+3*scale,stateY),new PointF(x,stateY+3*scale)});
            }
            if(Enabled&&Focused&&ShowFocusCues)using(var path=UiDrawing.Rounded(new RectangleF(3,3,Width-6,Height-6),Height/2f-3))using(var pen=new Pen(Muted)){pen.DashStyle=DashStyle.Dot;e.Graphics.DrawPath(pen,path);}
        }
    }
    public class BrandCredit:Control {
        public Color Ink=Color.White,Muted=Color.Gray,Accent=Color.SteelBlue;
        public BrandCredit(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleName="designed by Kechos";}
        protected override void OnPaint(PaintEventArgs e){
            using(var background=new SolidBrush(BackColor))e.Graphics.FillRectangle(background,ClientRectangle);
            using(var prefix=new Font("Segoe UI",9,FontStyle.Regular))using(var name=new Font("Segoe UI",9,FontStyle.Bold)){
                var flags=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix;
                int prefixWidth=TextRenderer.MeasureText(e.Graphics,"designed by",prefix,Size.Empty,flags).Width,nameWidth=TextRenderer.MeasureText(e.Graphics,"Kechos",name,Size.Empty,flags).Width;
                int gap=(int)Math.Ceiling(4*e.Graphics.DpiX/96f),inset=(int)Math.Ceiling(2*e.Graphics.DpiX/96f),x=Width-inset-prefixWidth-gap-nameWidth;
                TextRenderer.DrawText(e.Graphics,"designed by",prefix,new Rectangle(x,0,prefixWidth,Height),Muted,flags);TextRenderer.DrawText(e.Graphics,"Kechos",name,new Rectangle(x+prefixWidth+gap,0,nameWidth,Height),Ink,flags);
            }
        }
    }
    public class TrayRenderer:ToolStripProfessionalRenderer {
        readonly Color background,border,selected,ink,muted;
        public TrayRenderer(Color bg,Color line,Color hover,Color text,Color dim){background=bg;border=line;selected=hover;ink=text;muted=dim;RoundedEdges=false;}
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){using(var brush=new SolidBrush(background))e.Graphics.FillRectangle(brush,e.AffectedBounds);}
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){using(var brush=new SolidBrush(background))e.Graphics.FillRectangle(brush,e.AffectedBounds);}
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){using(var bg=new SolidBrush(background))e.Graphics.FillRectangle(bg,new Rectangle(Point.Empty,e.Item.Size));if(e.Item.Selected&&e.Item.Enabled){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var shape=UiDrawing.Rounded(new RectangleF(3,1,e.Item.Width-6,e.Item.Height-2),6))using(var fill=new SolidBrush(selected))e.Graphics.FillPath(fill,shape);}}
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=e.Item.Enabled||e.Item.Name=="brand"?ink:muted;base.OnRenderItemText(e);}
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(border))e.Graphics.DrawLine(pen,8,e.Item.Height/2,e.Item.Width-8,e.Item.Height/2);}
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var shape=UiDrawing.Rounded(new RectangleF(.5f,.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),8))using(var pen=new Pen(border))e.Graphics.DrawPath(pen,shape);}
    }

}