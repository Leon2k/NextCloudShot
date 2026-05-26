const path = require('path')
const { VueLoaderPlugin } = require('vue-loader')

module.exports = {
  entry: { 'nextcloudshot-main': path.resolve(__dirname, 'src', 'main.js') },
  output: {
    path: path.resolve(__dirname, 'js'),
    filename: '[name].js',
    clean: true,
  },
  module: {
    rules: [
      { test: /\.vue$/, loader: 'vue-loader' },
      { test: /\.s?css$/, use: ['style-loader', 'css-loader', 'sass-loader'] },
    ],
  },
  resolve: { extensions: ['.js', '.vue'] },
  plugins: [new VueLoaderPlugin()],
}
