# Ball Color Sort - ASM

## Mô tả Game

Ball Color Sort là một game giải đố thú vị nơi người chơi sắp xếp các viên bi màu vào các ống sao cho mỗi ống chỉ chứa một màu duy nhất.

## Thiết kế chính

### 1. Kiến trúc tổng quan
- Sử dụng mô hình MVC cơ bản:
  - **Model**: Quản lý trạng thái game (Tube.cs)
  - **View**: Hiển thị đồ họa và animation
  - **Controller**: Xử lý logic (GameController.cs)

### 2. Tính năng nổi bật
- **Hệ thống level tự động**: Tạo màn chơi ngẫu nhiên nhưng luôn có lời giải
- **Cơ chế di chuyển thông minh**: 
  - Cho phép di chuyển nhiều bi cùng màu một lúc
  - Animation mượt mà với hiệu ứng nối đuôi
- **Hỗ trợ đa độ phân giải**: Tự động điều chỉnh bố cục trên mọi màn hình

### 3. Công nghệ sử dụng
- Unity 2021.3+
- C# với các tính năng hiện đại (LINQ, Coroutine)

## Cài đặt
1. Clone repository
2. Mở bằng Unity 2021.3+
3. Build cho platform mong muốn

## Thành viên phát triển
- Lương Đức Hiếu (HieuDen05)

*Game được phát triển với mục đích học tập và nghiên cứu*
