   M S B A M L   `   `   `  ���� B  >WpfApp2, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null!clr-namespace:WpfApp2WpfApp2      WpfApp2.MainWindow   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     'localclr-namespace:WpfApp2     b ^SharpVectors.Converters.Wpf, Version=1.8.4.0, Culture=neutral, PublicKeyToken=b532964b8548be771svgc&http://sharpvectors.codeplex.com/svgc/  -   $
��Root��  � 
ResizeMode$  CanResizeWithGrip=� � Title$ 
MainWindow��$	��800��$
��1200�� � WindowStyle$
 None=� � AllowsTransparency . 
������ .��� -    �Name$ 
LayoutRoot��$��0,0,0,0q�.+�F� ���#ffffff
���   �$��1�� �� Stretch$ Uniform=� J� Opacity$	 1.0Q�$��Center=�$��Center=�����  �� ElementName$
 Root�� �� Path$
 Info��� $��	15,15,0,0q�$
��Left=�$	��Top=�	 �� Orientation$	 
Horizontal=�.+��� -   $��0,0,10,0q�$��25��$��25��
������ $��0q����� 
 �� 
TargetType"
 ��.R���  �� CornerRadius$ 25��#��z��._��� $��Center=�$��Center=�.��(  "SharpVectors.Converters.SvgViewbox  -     Name$	 Red������  �� 
TargetType"  .���  �� Property �@ Source �� Value$ /icon/Close_Button.svg?� �� Triggers P�  P� Property �@J�IsMouseOver P� Value . . ���  �@ Source$" /icon/Close_Button_Hover.svg?���  �� Binding �� $ Yellow��$ IsMouseOver� �� Value True.a���  �@ Source$" /icon/Close_Button_Hover.svg?���  �� $ Green��$ IsMouseOver� True.a���  �@ Source$" /icon/Close_Button_Hover.svg?��� -   $��0,0,10,0q�$��25��$��25��
������ $��0q����� "
 ��.R��� $ 25��#��z��._��� $��Center=�$��Center=�.��  -   $ Yellow������ "  .���  �@ Source$ /icon/Minimize_Button.svg?� P�  �@J�IsMouseOver . . ���  �@ Source$% /icon/Minimize_Button_Hover.svg?���  �� $	 Red��$ IsMouseOver� True.a���  �@ Source$% /icon/Minimize_Button_Hover.svg?���  �� $ Green��$ IsMouseOver� True.a���  �@ Source$% /icon/Minimize_Button_Hover.svg?��� -   $��25��$��25��
������ $��0q����� "
 ��.R��� $ 25��#��z��._��� $��Center=�$��Center=�.��  -   $ Green������ "  .���  �@ Source$ /icon/Maximize_Button.svg?� P�  �@J�IsMouseOver . . ���  �@ Source$% /icon/Maximize_Button_Hover.svg?���  �� $	 Red��$ IsMouseOver� True.a���  �@ Source$% /icon/Maximize_Button_Hover.svg?���  �� $ Yellow��$ IsMouseOver� True.a���  �@ Source$% /icon/Maximize_Button_Hover.svg?�c� $
��Left=�$	��100��$��332,231,0,0q�$	��Top=�$	��100�� J� RenderTransformOrigin$ 0.5,0.5&�z�V� .~��� �� ��  �� Angle$ -457.169Q�T� 