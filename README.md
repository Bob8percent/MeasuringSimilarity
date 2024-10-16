# メッシュの類似度の計算、差分表示を行うアプリケーション
Unityの勉強の一環として、2つのメッシュの類似度計算と、それらの差分表示をリアルタイムに行うアプリケーションを作ってみました。


## 使用例
1. まず、**正解のメッシュ**と**比較対象のメッシュ**を用意します。

下の画像は実行画面ですが、左のメッシュが**比較対象**、右のメッシュが**正解**です。

用意したこれらのメッシュは同じものなので類似度は100%となっています。

![Image](https://github.com/Bob8percent/MeasuringSimilarity/blob/master/Document/MeasuringSimilarity1.png)

2. 比較対象のメッシュの頂点をいじってみると、下の動画のように差分が赤くハイライトされ、類似度がリアルタイムで再計算されます。

[![Watch the video](https://img.youtube.com/vi/2nKDbyTqQR0/maxresdefault.jpg)](https://www.youtube.com/watch?v=2nKDbyTqQR0)

※ 頂点の編集にはVertex Tweakerを使用しています

## 仕組み
### 類似度の計算方法
1. **メッシュの重ね合わせ**

比較対象のメッシュを正解のメッシュとできるだけ誤差が小さくなるように重ね合わせます。

この重ね合わせには、ShapeMatching法(*1) という技術を用いました。

2. **メッシュのボクセル化**

類似度を計算しやすくするために、それぞれのメッシュをボクセル化(*2)します。

ボクセル化はCPUで愚直に計算すると極端に重くなるので、コンピュートシェーダーを記述し**GPU**で計算しております。

3. **類似度の計算**

類似度は、次の式で計算しました。(*3)

`(重なっているボクセル集合の総体積 A∩B) ÷ (2つのボクセル集合の合計体積 A∪B)`


### 差分の表示方法

差分表示は、比較対象のメッシュの各三角形(ポリゴン)に色を塗ることで実現しています。

正解のメッシュと**類似している部分は水色**、**異なっている部分は赤色**で描画されます。

**比較メッシュの各三角形**について、次の式で類似度を計算しました。

`(三角形と正解メッシュのボクセルが重なる数) ÷ (三角形と比較メッシュのボクセルが重なる数)`

これを0から1の間でClampし、**0に近いほど水色、1に近いほど赤色**となるように描画しています。

## 参照文献

(*1) ShapeMatching法

参考: IndieVisualLab, Unity Graphics Programming vol.2, 第7章 Shape Matching - 線形代数のCGへの応用 -

(*2) ボクセル化

参考: IndieVisualLab, Unity Graphics Programming vol.2, 第1章 Real-Time GPU-Based Voxelizer

(*3) Intersection over Union; IoU

参考: https://qiita.com/CM_Koga/items/82d446658957d51836cf

## 実行環境
Windows 11

16GB RAM

11th Gen Intel(R) Core(TM) i9-11900H @ 2.50GHz

NVIDIA GeForce RTX 3050 Laptop GPU

Unity 2022.3.49f1

## ライセンス
このアプリケーションでは、[Utah Teapot PBR (hackmans)](https://sketchfab.com/3d-models/utah-teapot-pbr-185b612a223d4dd5b03e55376429574f)によって提供された3Dモデルを使用しています。このモデルは、Creative Commons Attribution 4.0 International License のもとでライセンスされています。
