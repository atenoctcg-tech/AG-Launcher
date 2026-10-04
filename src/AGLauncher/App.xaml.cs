using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace AGLauncher;
public partial class App : Application
{
 public static bool MotionEnabled { get; set; } = true;
 private void Panel_MouseEnter(object sender, MouseEventArgs e) => Zoom(sender,1.012);
 private void Panel_MouseLeave(object sender, MouseEventArgs e) => Zoom(sender,1);
 private static void Zoom(object sender,double scale)
 {
  if(sender is not FrameworkElement element) return;
  if(element.RenderTransform is not ScaleTransform transform || transform.IsFrozen) element.RenderTransform=transform=new ScaleTransform(1,1);
  var enabled=MotionEnabled && SystemParameters.ClientAreaAnimation;
  foreach(var property in new[]{ScaleTransform.ScaleXProperty,ScaleTransform.ScaleYProperty})
   transform.BeginAnimation(property,new DoubleAnimation(enabled?scale:1,TimeSpan.FromMilliseconds(enabled?190:0)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
 }
 public static void Reveal(FrameworkElement element)
 {
  if(!MotionEnabled || !SystemParameters.ClientAreaAnimation) return;
  element.RenderTransformOrigin=new Point(.5,.5);
  var t=new ScaleTransform(1,1);element.RenderTransform=t;
  var a=new DoubleAnimation(.987,1,TimeSpan.FromMilliseconds(240)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
  t.BeginAnimation(ScaleTransform.ScaleXProperty,a);t.BeginAnimation(ScaleTransform.ScaleYProperty,a);
  element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.4,1,TimeSpan.FromMilliseconds(220)));
 }
}
