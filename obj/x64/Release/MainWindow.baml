   M S B A M L   `   `   `  ���� D  @WinFinder, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null%clr-namespace:WinFinder	WinFinder  P LVirtualizingWrapPanel, Version=2.1.1.0, Culture=neutral, PublicKeyToken=nullX@clr-namespace:WpfToolkit.Controls;assembly=VirtualizingWrapPanelWpfToolkit.Controls     WinFinder.MainWindow   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     )localclr-namespace:WinFinder     b ^SharpVectors.Converters.Wpf, Version=1.8.4.2, Culture=neutral, PublicKeyToken=b532964b8548be771svgc&http://sharpvectors.codeplex.com/svgc/  Jvwp@clr-namespace:WpfToolkit.Controls;assembly=VirtualizingWrapPanel  -   $
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
ZoomButton� �� RelativeSource ��  �� Mode$ FindAncestor=� �� AncestorType" ��� $ 
Horizontal=�$
��Left=�$��	200,0,0,0q�$��Center=�.+��� -    ��Name$ PathBack��$��0,0,0,0q�$��0q� Z� ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Center=�$��Center=���  � �� �#ffffff P�  P� Property �@J�IsMouseOver P� Value . . ���  � �� �#9ABAE8.��  $��20��$��20��    Source$  /icon/left.svg?��� -   $ PathMove��$��5,0,0,0q�$��0q� Z� ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Center=�$��Center=���  � �� �#ffffff P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8.��  $��20��$��20��$  /icon/right.svg?��� -   $ ViewListView��$��5,0,0,0q�$��0q� Z� ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Center=�$��Center=���  � �� �#ffffff P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8.��  $��20��$��20��$  /icon/list.bullet.svg?��� -   $ ViewGridView��$��5,0,0,0q�$��0q� Z� ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Center=�$��Center=���  � �� �#ffffff P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8.��  $��20��$��20��$&   /icon/rectangle.on.rectangle.svg?��� -   ! ��Name$! currentFolderRegion��
������ $��5,0,0,0q�" �� Padding$" 	10,0,10,0q�$��NoWrap=�$��CharacterEllipsis=�$
��Left=�$��Center=�$��21(�$��Regular"�$%��霞鹜新晰黑 屏幕阅读版)� Z� ���� $
 Root��$ CurrentFolder�� $��1��# � ColumnDefinitions# �� $	��200 ��� $��* �.+�� -   $ LeftSideOutside��$��0�����#EfEfEf.+��� $
 0��$ 0��$ 0��$��Right=�
���   �$	��0.5�� �� $ LeftSideOutside��$ ActualHeight�� -   $ LeftSide��	 �� $��40 ��� $��* �.+��� $��0��$��常用目录��$��Center=�$	��Top=�$��0,10,0,0q�$ J� Opacity$	$ 0.7Q�$��18(�$%��霞鹜新晰黑 屏幕阅读版)�� $��1��.+�F� -   $ SideClip������ $
 Root��$ SideClipInfo��� -   $ SideBar��$��6,0,6,10q�$ Vertical=�� $��1�����#FFFFFF	 �� $��* ��� $��30 ��� $��42 �.+�F� -   $ FileClip������ $
 Root��$ ClipInfo�F� -   $ GridViewClip������ $
 Root��$ GridClipInfo�� $��0��$��6,0,6,0q�.+�� -   $ RefGrid��# �� $��5* ��� $��2* ��� $	��120 ��� $	��130 �.+�� -   $ late0��$��0��� -   $ late1��$��1��� -   $ late2��$��2��� -   $ late3��$��3���� -   $��FILEINFOMATION��% %� IsVirtualizing% . & %� 
ScrollUnit$& Pixel=�' %� VirtualizationMode$' 	Recycling=�( %� CacheLength@  :System.Windows.Controls.VirtualizationCacheLengthConverter$( 200, 200 ) %� CacheLengthUnit$) Pixel=�$��Disabled=�$��Hidden=�$��0,0,0,0q�$��0,0,0,0q�$��0q�* �� AlternationCount$* 2��$
��Left=� Z� ���� *ActualWidth+$ RefGrid������ " ��.���  � �� �� " ��.R��� + �� Padding#+ z��#��z��#��z��#��z��, J� SnapsToDevicePixels, . ._��� #��z��#��z��#, z, ��  � ��$ 0,0,0,0q���  � ��$ Stretch=���  � ��$ 30����  � �� �#000000��  � ��$ 0q���  �@J�Clip �� *Data+$ FileClip��-    P�  �@��AlternationIndex$ 1��. ���  � �� �#F5F5F5P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8P�  �@��
IsSelected . . ���  � �� �#2962D9- �� View- �� .P��� . �� Width. �� *ActualWidth+$ late0��.���� $��30��
������ ���� " ��.���  � �� �� " ��.R��� #��z��._��� $
��Left=�$��Center=���  � y�$ 	Collapsed=�.���� $��名称��$��21(�$%��霞鹜新晰黑 屏幕阅读版)�$
��Bold"�$��10,0,0,0q�/ �� CellTemplate/ �� .R��� $ 
Horizontal=�$��Center=�.+��� $��25��$��25������ *S0+0 �� IsAsync0 . �� $��5,0,0,0q�$��15(�$%��霞鹜新晰黑 屏幕阅读版)�$��Stretch=�$��Center=����� *X0+�� . �� *ActualWidth+$ late1��.���� $��30��
������ ���� " ��.���  � �� �� " ��.R��� #��z��._��� $
��Left=�$��Center=���  � y�$ 	Collapsed=�.���� $��日期��$��21(�$%��霞鹜新晰黑 屏幕阅读版)�$
��Bold"�$��12,0,0,0q�/ �� .R��� $%��霞鹜新晰黑 屏幕阅读版)�$��15(�$��5,0,0,0q�$��Stretch=�$��Center=�$	$ 0.7Q����� *X1+�� . �� *ActualWidth+$ late2��.���� $��30��
������ ���� " ��.���  � �� �� " ��.R��� #��z��._��� $
��Left=�$��Center=���  � y�$ 	Collapsed=�.���� $��类型��$��21(�$%��霞鹜新晰黑 屏幕阅读版)�$
��Bold"�$��12,0,0,0q�/ �� .R��� $%��霞鹜新晰黑 屏幕阅读版)�$��15(�$��5,0,0,0q�$
��Left=�$��Center=�$	$ 0.7Q����� *X2+�� . �� *ActualWidth+$ late3��.���� $��30��
������ ���� " ��.���  � �� �� " ��.R��� #��z��._��� $��Right=�$��Center=���  � y�$ 	Collapsed=�.���� $��大小��$��21(�$%��霞鹜新晰黑 屏幕阅读版)�$
��Bold"�$��0,0,20,0q�/ �� .R��� $%��霞鹜新晰黑 屏幕阅读版)�$��15(�$��0,0,15,0q�$��Right=�$��Center=�$	$ 0.7Q����� *X3+
1 � Tag1 �� *X4+2  ,WpfToolkit.Controls.VirtualizingItemsControl  -    2  Name$2 GridViewContainer��$y�	Collapsed=����� " ��.R��� -!   $��Hidden=� Z� .���� ���� .R�/  )WpfToolkit.Controls.VirtualizingWrapPanel  3   SpacingMode$3 BetweenItemsOnly=�|�.  4 %� IsContainerVirtualizable4 . % . $' 	Recycling=�$& Pixel=�$( 200, 200 $) Pixel=�5   Orientation$5 
Horizontal=�$��0,6,0,6q����� .R�� 	 �� $	��105 ��� $��* ����� " �.���  � ��$	 115����  � ��$	 150����  �@J�Clip �� *Data+$ GridViewClip��-"    P�  �@J�IsMouseOver . . ���  � �� �#9ABAE8.+��� $��0��$	��105��$	��100��$��Center=�$��Bottom=����� *S0+0 . �� $��1��$��5,0,5,0q�$��15(�$%��霞鹜新晰黑 屏幕阅读版)�6 �� TextAlignment$6 Center=�$
��Wrap=�$��CharacterEllipsis=�$��Center=�$	��Top=����� *X0+1 �� *X2+� $��1��.+��� $
 0��$ 0��$	��Top=�
���   �$	��0.5�� �� $ 	pwdRegion��$ ActualWidth��� -#   $! 	ExtraInfo��$��0,0,0,0q�$" 	10,0,10,0q�$��NoWrap=�$��CharacterEllipsis=�$��Center=�$��Center=�$��15(�$��Regular"�$%��霞鹜新晰黑 屏幕阅读版)� Z� � -$   $ 	pwdRegion��$��2��.+��� $
 0��$ 0��$	��Top=�
���   �$	��0.5�� �� $ 	pwdRegion��$ ActualWidth��� -%   $ pwdInfo��$��6,0,0,0q�
������ $
��Left=�$��Center=�$ 
Horizontal=�