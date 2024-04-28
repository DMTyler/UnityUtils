因为开发过程中经常要用到这些东西，所以就整理了一下，
一套基于 Unitask 与 Addressable 的基础性实用框架，
包含：
1. 简单的匿名函数绑定 Action / UnityEvent 与解绑 TODO：再检查一下性能问题，确认使用完的匿名函数没有被引用
2. 状态机 TODO：可视化
3. 行为树 TODO：可视化
4. 游戏中状态存储（用于撤销行为，不用于存档）
5. 可控携程
6. 单例
7. 基于 Addressable 的资源管理器
8. 简单的 UI 管理 TODO: 增加预加载，string映射改为type映射
....

TODO:
1. 完善 Decoration 相关逻辑
2. 实现一个完全深拷贝（Deepcopy实例且实例中的所有引用对象及其迭代子引用对象都被拷贝），但是现有思路需求 unsafe， 考虑结合 C++ dll 能否绕过unsafe？（object 在 IL 中可以直接转换为 void*）
