A simple reimplementation of BRDF in UE4 in Unity Urp.
一个对 UE4 中的 BRDF 渲染的 Unity Urp 简单再现。

**IMPORTANT**:
**重要**：
This reimplementation is for study only. It is not well-tested and not suitable for any kind of projects.
这个再现仅作为学习目的使用。并未经过测试且不适用与任何类型的项目。

How to use:
如何使用:
1. Bind Script "IntegrateBaker" to a gameObject. Then bind the file "IntegrateComputer.compute" to the field "IntegrateBaker.computeShader.
将 "IntegrateBaker" 绑定至一个物体。随后将 IntegrateComputer.compute 绑定至 “IntegrateBaker.computeShader” 字段。

2. Call the function IntegrateBaker.Bake(). This will bake integration map in certain path and automatically set integration map in all relative shaders.
调用 IntegrateBaker.Bake() 函数。这会在指定路径下创建 integrate map 并自动绑定至所有相关 shader 。

3. Create material with shader ”Custom/Lit“ or "Custom/Lit_Normal" if you want to enable normal map.
使用 “Custom/Lit” shader 创建材质。如果你想使用法线贴图，使用 “Custom/Lit_Normal”。

4. Change the material of object with material you create in step3. Then bind script "EnvMapBaker" to the same object.
将步骤 3 中创建的材质绑定至物体。在相同物体上绑定脚本 “EnvMapBaker”。

5. Call the function "EnvMapBaker.BakeDiffuseMap()" & "EnvMapBaker.BakeSpecularMap()" to bake environment map, IBL diffuse map and IBL specular map. This step may take longer time depending on how good your CPU is.
调用函数 “EnvMapBaker.BakeDiffuseMap()” 与 “EnvMapBaker.BakeSpecularMap()” 来创建环境贴图，IBL 散射贴图 与 IBL 高光贴图。依照CPU的好坏，这可能会花费较长的时间.
*In this step, the environment map is baked based on the position of the object in default. You may bind the environment map manually before bake.
*在这一步中，环境贴图默认基于物体位置创建，你也可以手动绑定环境贴图

6. Once the bake in step 5 is finished, _Diffusemap & _Specularmaps in material shall be automatically binded. If doesn't, you still can bind it manually since all textures(cubemaps in this case) baked will be saved on disk.
步骤 5 中的烘焙结束后，材质中的 _Diffusemap 与 _Specularmaps 应当被自动绑定。倘若没有，你仍然可以自己绑定，因为所有烘焙好的纹理都会被保存在硬盘上


TODO:
懒得翻译了：
1. The binding in step 2 is not permanent. Although integration map will be permanently stored on disk, it is not the same for texture field in shader. This requires a rebinding every time after the memory is clear.
2. The bake in step 5 is using CPU. This is extremely inefficient and thus time-consuming.
