A simple script to convert textures into pixel-style
> Support two ways of converting: voting (choose the most frequent color in an area) and averaging (calculating the average color in an certain area)
> Support decompress unity dtx1 & dtx5 (that is, low quality compression & normal quality compression in Unity) automatically using squish, check squish at: https://oblivioncth.github.io/libsquish/#:~:text=The%20squish%20library%20(abbreviated%20to%20libsquish)%20is%20an%20open
> Tested only under URP and only texture format with RGBA32, RGBA64, RGBAFloat
> **IMPORTANT**: currently **Support Default Texture Type Only**, if you want to bake normal maps, please change their types into default first before baking.

TODO:
> Decompression of high quality compression
> A convienient way to bake normal maps
