   M S B A M L   `   `   `  ���� D  @WinFinder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null%clr-namespace:WinFinder	WinFinder  P LVirtualizingWrapPanel, Version=2.1.1.0, Culture=neutral, PublicKeyToken=nullX@clr-namespace:WpfToolkit.Controls;assembly=VirtualizingWrapPanelWpfToolkit.Controls     WinFinder.MainWindow   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     )localclr-namespace:WinFinder     b ^SharpVectors.Converters.Wpf, Version=1.8.4.2, Culture=neutral, PublicKeyToken=b532964b8548be771svgc&http://sharpvectors.codeplex.com/svgc/  Jvwp@clr-namespace:WpfToolkit.Controls;assembly=VirtualizingWrapPanel  -   $
��Root��  � 
ResizeMode$  NoResize=� � Title$ 
MainWindow��$	��902��$
��1202�� � WindowStyle$
 None=� � AllowsTransparency . 
������ .��� -   .+�F� -    F�Name$ ClipPath��
���   �$	��1.0������  �� ElementName$
 Root�� �� Path$
 Info�� -    �Name$ LayoutRootParent��}�. $��0,0,0,0q����#ffffff J� Clip �� $ ClipPath��$
 Data�.+�� -   $ 
LayoutRoot��$��1,1,1,1q�	 � RowDefinitions	 �� $��46 ��� $��* �.+�� -   $ LayoutNodeTop��$��0�����#F4F3F2.+��� 	
 �� X1$
 0��	 �� Y1$ 0��	 �� Y2$ 0��$��Bottom=�
���   �$	��0.5��	 �� X2 �� $ LayoutNodeTop��$ ActualWidth��� -    ��Name$ Traffic��$��	13,13,0,0q����#EEEEEE$
��Left=�$	��Top=� �� Orientation$ 
Horizontal=�.+��� -   $��0,0,15,0q�$��20��$��20�����#ffffff$��0q� � FocusVisualStyle Z� ����  �� 
TargetType" ��.R���  �� CornerRadius$ 20��#��z��._��� $��Center=�$��Center=�.��(  "SharpVectors.Converters.SvgViewbox  -	     Name$	 Red������  �� 
TargetType"  .���  �� Property �@ Source �� Value$ /icon/Close_Button.svg?� �� Triggers ��  �� Binding �� $ Traffic��$ IsMouseOver� �� Value True.a���  �@ Source$% /icon/Close_Button_Hover_M2.svg?��� -
   $��0,0,15,0q�$��20��$��20�����#ffffff$��0q� Z� ���� " ��.R��� $ 20��#��z��._��� $��Center=�$��Center=�.��  -   $ Yellow������ "  .���  �@ Source$ /icon/Minimize_Button.svg?� ��  �� $ Traffic��$ IsMouseOver� True.a���  �@ Source$' !/icon/Minimize_Button_Hover_M.svg?��� -   $��20��$��20�����#ffffff$��0q� Z� ���� " ��.R��� $ 20��#��z��._��� $��Center=�$��Center=�.��  -   $ Green������ "  .���  �@ Source$ /icon/Maximize_Button.svg?� ��  �� $ Traffic��$ IsMouseOver� True.a���  �@ Source �� $ 
ZoomButton� �� RelativeSource ��  �� Mode$ FindAncestor=� �� AncestorType" ��� -    ��Name$ PathBack��$��	200,0,0,0q�$��0q� Z� ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Center=�$��Center=���  � ��
 ����  P�  P� Property �@J�IsMouseOver P� Value . . ���  � �� �#9ABAE8.��  $��25��$��25��    Source$  /icon/arrow.up.app.svg?��� -   $ PathMove��$��	235,0,0,0q�$��0q� Z� ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Center=�$��Center=���  � ��
 ����  P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8.��  $��25��$��25��$  /icon/arrow.right.app.svg?��� -   ! ��Name$! currentFolderRegion��
������ " �� Padding$" 	10,0,10,0q�$��NoWrap=�$��CharacterEllipsis=�$	��325��$
��Left=�$��	280,0,0,0q�$��Center=�$��21(�$��Regular"�$%��霞鹜新晰黑 屏幕阅读版)� Z� ���� $
 Root��$ CurrentFolder��� -   $ ViewListView��$��	610,0,0,0q�$��0q� Z� ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Center=�$��Center=���  � ��
 ����  P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8.��  $��25��$��25��$  /icon/list.bullet.svg?��� -   $ ViewGridView��$��	645,0,0,0q�$��0q� Z� ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Center=�$��Center=���  � ��
 ����  P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8.��  $��25��$��25��$&   /icon/rectangle.on.rectangle.svg?�� $��1��# � ColumnDefinitions# �� $	��200 ��� $��* �.+�� -   $ LeftSide��$��0�����#E1E1E1.+��� $
 0��$ 0��$ 0��$��Right=�
���   �$	��0.5�� �� $ LeftSide��$ ActualHeight�F� -   $ SideClip������ $
 Root��$ SideClipInfo��� -   $ SideBar��$��	6,10,6,10q�$ Vertical=�� $��1�����#FFFFFF	 �� $��* ��� $��36 �.+�F� -   $ FileClip������ $
 Root��$ ClipInfo�F� -   $ GridViewClip������ $
 Root��$ GridClipInfo�� $��0��$��6,0,6,0q�.+�� -   $ RefGrid��# �� $��5* ��� $��2* ��� $	��120 ��� $	��130 �.+�� -   $ late0��$��0��� -   $ late1��$��1��� -   $ late2��$��2��� -   $ late3��$��3���� -   $��FILEINFOMATION��$ %� IsVirtualizing$ . % %� 
ScrollUnit$% Pixel=�& %� VirtualizationMode$& 	Recycling=�' %� CacheLength@  :System.Windows.Controls.VirtualizationCacheLengthConverter$' 200, 200 ( %� CacheLengthUnit$( Pixel=�$��Disabled=�$��Hidden=�$��0,0,0,0q�$��0,0,0,0q�$��0q�) �� AlternationCount$) 2��$
��Left=� Z� ���� *ActualWidth+$ RefGrid������ " ��.���  � �� �� " ��.R��� * �� Padding#* z��#��z��#��z��#��z��+ J� SnapsToDevicePixels+ . ._��� #��z��#��z��#+ z+ ��  � ��$ 0,0,0,0q���  � ��$ Stretch=���  � ��$ 30����  � �� �#000000��  � ��$ 0q���  �@J�Clip �� *Data+$ FileClip��-    P�  �@��AlternationIndex$ 1��. ���  � �� �#F5F5F5P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8P�  �@��
IsSelected . . ���  � �� �#2962D9, �� View, �� .P��� - �� Width- �� *ActualWidth+$ late0��.���� $��30��
������ ���� " ��.���  � �� �� " ��.R��� #��z��._��� $
��Left=�$��Center=�.���� $��名称��$��21(�$%��霞鹜新晰黑 屏幕阅读版)�$
��Bold"�$��10,0,0,0q�. �� CellTemplate. �� .R��� $ 
Horizontal=�$��Center=�.+��� $��25��$��25������ *S0+/ �� IsAsync/ . �� $��5,0,0,0q�$��15(�$%��霞鹜新晰黑 屏幕阅读版)�$��Stretch=�$��Center=����� *X0+�� - �� *ActualWidth+$ late1��.���� $��30��
������ ���� " ��.���  � �� �� " ��.R��� #��z��._��� $
��Left=�$��Center=�.���� $��日期��$��21(�$%��霞鹜新晰黑 屏幕阅读版)�$
��Bold"�$��12,0,0,0q�. �� .R��� $%��霞鹜新晰黑 屏幕阅读版)�$��15(�$��5,0,0,0q�$��Stretch=�$��Center=�0 J� Opacity$	0 0.7Q����� *X1+�� - �� *ActualWidth+$ late2��.���� $��30��
������ ���� " ��.���  � �� �� " ��.R��� #��z��._��� $
��Left=�$��Center=�.���� $��类型��$��21(�$%��霞鹜新晰黑 屏幕阅读版)�$
��Bold"�$��12,0,0,0q�. �� .R��� $%��霞鹜新晰黑 屏幕阅读版)�$��15(�$��5,0,0,0q�$
��Left=�$��Center=�$	0 0.7Q����� *X2+�� - �� *ActualWidth+$ late3��.���� $��30��
������ ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Right=�$��Center=�.���� $��大小��$��21(�$%��霞鹜新晰黑 屏幕阅读版)�$
��Bold"�$��0,0,20,0q�. �� .R��� $%��霞鹜新晰黑 屏幕阅读版)�$��15(�$��0,0,15,0q�$��Right=�$��Center=�$	0 0.7Q����� *X3+
1 � Tag1 �� *X4+2  ,WpfToolkit.Controls.VirtualizingItemsControl  -   2  Name$2 GridViewContainer��$y�	Collapsed=����� " ��.R��� -    $��Hidden=� Z� .���� ���� .R�/  )WpfToolkit.Controls.VirtualizingWrapPanel  3   SpacingMode$3 BetweenItemsOnly=�|�.  4 %� IsContainerVirtualizable4 . $ . $& 	Recycling=�$% Pixel=�$' 200, 200 $( Pixel=�5   Orientation$5 
Horizontal=�$��0,6,0,6q����� .R�� 	 �� $	��110 ��� $��* ����� " �.���  � ��$	 115����  � ��$	 150����  �@J�Clip �� *Data+$ GridViewClip��-!    P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8.+��� $��0��$	��105��$	��105��$��Center=�$��Bottom=����� *S0+/ . �� $��1��$��5,0,5,0q�$��15(�$%��霞鹜新晰黑 屏幕阅读版)�6 �� TextAlignment$6 Center=�$
��Wrap=�$��CharacterEllipsis=�$��Center=�$	��Top=����� *X0+1 �� *X2+� -"   $ 	pwdRegion��$��1��.+��� $
 0��$ 0��$	��Top=�
���   �$	��0.5�� �� $ 	pwdRegion��$ ActualWidth��� -#   $ pwdInfo��$��17,0,0,0q�
������ $
��Left=�$��Center=�$ 
Horizontal=�