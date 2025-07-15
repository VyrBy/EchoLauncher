import tkinter as tk
from tkinter import ttk, messagebox, simpledialog
import minecraft_launcher_lib
from minecraft_launcher_lib import utils, install
from minecraft_launcher_lib.fabric import install_fabric, get_all_minecraft_versions as get_fabric_versions
import requests, json, uuid, os, subprocess, webbrowser
from PIL import Image, ImageTk
import sys
from io import BytesIO



# === Константы ===
AUTH_URL               = "https://authserver.ely.by/auth/authenticate"            # url с аутентификацией
REFRESH_URL            = "https://authserver.ely.by/auth/refresh"                 # хз, важная херня
AUTHLIB_INJECTOR_PATH  = "authlib/authlib-injector-1.2.5.jar"                     # путь к AuthLib для ely.by

if getattr(sys, 'frozen', False):
    LAUNCHER_DIR = os.path.dirname(sys.executable)
else:
    LAUNCHER_DIR = os.path.abspath(os.path.dirname(__file__))

GAME_ROOT_DIR          = os.path.join(LAUNCHER_DIR, "instances")                  # общая папка игры
BUILDS_DIR             = os.path.join(GAME_ROOT_DIR, "builds")                    # сюда кладём сборки
SESSION_DIR            = os.path.join(LAUNCHER_DIR, "session")                    # папка сессии
SESSION_FILE           = os.path.join(SESSION_DIR, "session.json")                # файл сессии
BUILDS_FILE            = os.path.join(GAME_ROOT_DIR, "builds.json")               # файл сборок
JAVA_CONFIG_FILE       = os.path.join(LAUNCHER_DIR, "java_config.json")           # конфиг жавы, там сохраняются указанные параметры
#получаем путь к жаве
def get_java_path(build_path):
    runtime_base = os.path.join(build_path, "runtime")
    candidates = ["java-runtime-gamma", "java-runtime-delta"]

    for candidate in candidates:
        java_path = os.path.join(
            runtime_base,
            candidate,
            "windows-x64",
            candidate,
            "bin",
            "java.exe"
        )
        if os.path.isfile(java_path):
            return java_path

    # Если ни одна из версий не найдена
    raise FileNotFoundError("Java runtime не найден ни в gamma, ни в delta")


#создаём папки, если надо
os.makedirs(GAME_ROOT_DIR, exist_ok=True)
os.makedirs(BUILDS_DIR, exist_ok=True)
os.makedirs(SESSION_DIR, exist_ok=True)

# === Доступные версии Minecraft ===
#ванилла
vanilla_versions   = utils.get_available_versions(GAME_ROOT_DIR)
vanilla_version_ids = [v["id"] for v in vanilla_versions if v["type"] in ["release", "snapshot"]]
#фарбик
fabric_versions_raw = get_fabric_versions()
fabric_version_ids  = [v["version"] for v in fabric_versions_raw]

# === Java конфиг ===
#загрузка конфига жавы
def load_java_config():
    if not os.path.isfile(JAVA_CONFIG_FILE):
        return {"memory": "2G", "args": ""}
    with open(JAVA_CONFIG_FILE, encoding="utf-8") as f:
        return json.load(f)
#сохранение конфига
def save_java_config(cfg):
    with open(JAVA_CONFIG_FILE, "w", encoding="utf-8") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)

java_config = load_java_config()

# Forge promos подгружаем на лету, когда нужно
forge_promos_cache = {}  # mc_version -> forge_version

def fetch_forge_promos():
    global forge_promos_cache
    if forge_promos_cache:
        return
    try:
        data = requests.get("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json").json()
        promos = data.get("promos", {})
        for key, val in promos.items():
            if key.endswith("-latest"):
                mc = key[:-7]
                forge_promos_cache[mc] = val
    except Exception as e:
        print("[Forge] Не удалось получить promos:", e)

# === Сессия ===
#сохранение сессии
def save_session(d):
    with open(SESSION_FILE, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=2)
#загрузка сесии
def load_session():
    if not os.path.isfile(SESSION_FILE):
        return None
    with open(SESSION_FILE, encoding="utf-8") as f:
        return json.load(f)

# === Сборки ===
#загрузка существующих сборок
def load_builds():
    if not os.path.isfile(BUILDS_FILE):
        return []
    with open(BUILDS_FILE, encoding="utf-8") as f:
        return json.load(f)
#сохранение сборки
def save_builds(builds):
    with open(BUILDS_FILE, "w", encoding="utf-8") as f:
        json.dump(builds, f, ensure_ascii=False, indent=2)
#создание сборки
def add_build(name, mc_version, mc_type):
    builds = load_builds()
    if any(b["name"] == name for b in builds):
        raise ValueError("Сборка с таким именем уже существует")
    build = {"name": name, "version": mc_version, "type": mc_type}
    builds.append(build)
    save_builds(builds)
    # создаём папку
    os.makedirs(os.path.join(BUILDS_DIR, name), exist_ok=True)

# === Авторизация ===

def login():
    user, pwd = username_entry.get(), password_entry.get()
    if not user or not pwd:
        return messagebox.showwarning("Ошибка", "Введите логин и пароль")
    payload = {"username": user, "password": pwd, "clientToken": str(uuid.uuid4()), "requestUser": True}
    try:
        r = requests.post(AUTH_URL, json=payload)
        if r.status_code == 200:
            d = r.json()
            sess = {"accessToken": d["accessToken"], "clientToken": d["clientToken"],
                    "uuid": d["selectedProfile"]["id"], "username": d["selectedProfile"]["name"]}
            save_session(sess)
            messagebox.showinfo("Готово", f"Добро пожаловать, {sess['username']}!")
        else:
            messagebox.showerror("Ошибка", r.json().get("errorMessage", "Неизвестная ошибка"))
    except Exception as e:
        messagebox.showerror("Ошибка", str(e))

def refresh_session():
    sess = load_session()
    if not sess:
        return messagebox.showwarning("Нет токена", "Сначала войдите через логин и пароль.")
    payload = {"accessToken": sess["accessToken"], "clientToken": sess["clientToken"], "requestUser": True}
    try:
        r = requests.post(REFRESH_URL, json=payload)
        if r.status_code == 200:
            d = r.json()
            sess.update({"accessToken": d["accessToken"],
                          "uuid": d["selectedProfile"]["id"],
                          "username": d["selectedProfile"]["name"]})
            save_session(sess)
            messagebox.showinfo("Сессия", f"Добро пожаловать обратно, {sess['username']}!")
        else:
            messagebox.showerror("Ошибка", r.json().get("errorMessage", "Неизвестная ошибка"))
    except Exception as e:
        messagebox.showerror("Ошибка", str(e))

# === Установка версий (vanilla / fabric / forge) ===
#убеждаемся в том, что сборка установлена
def ensure_installed(build):
    mc_version = build["version"]
    mc_type = build["type"]
    game_dir = os.path.join(BUILDS_DIR, build["name"])
    version_id = None

    versions_dir = os.path.join(game_dir, "versions")
    if not os.path.exists(versions_dir):
        os.makedirs(versions_dir)

    #когда версия уже установлена
    def version_installed(ver_id):
        return os.path.exists(os.path.join(versions_dir, ver_id))

    #если ванилла
    if mc_type == "vanilla":
        version_id = mc_version
        if not version_installed(version_id):
            install.install_minecraft_version(mc_version, game_dir)

    #если фабрик
    elif mc_type == "fabric":
        install.install_minecraft_version(mc_version, game_dir)
        install_fabric(mc_version, game_dir)
        version_id = None
        for d in os.listdir(versions_dir):
            if d.endswith(f"-{mc_version}") and "fabric-loader" in d:
                version_id = d
                break
        if not version_id:
            raise Exception("Не удалось найти установленную Fabric версию")

    #если форж
    elif mc_type == "forge":
        # Получаем forge версию по mc_version
        forge_version = minecraft_launcher_lib.forge.find_forge_version(mc_version)
        if forge_version is None:
            raise Exception(f"Forge не поддерживает версию {mc_version}")

        if not minecraft_launcher_lib.forge.supports_automatic_install(forge_version):
            raise Exception(f"Автоматическая установка Forge {forge_version} не поддерживается")

        minecraft_launcher_lib.forge.install_forge_version(forge_version, game_dir)

        # Найдём установленную версию (поиск по versions/*)
        all_installed = utils.get_installed_versions(game_dir)
        for v in all_installed:
            if mc_version in v["id"] and "forge" in v["id"]:
                version_id = v["id"]
                break
        else:
            raise Exception("Forge установлен, но не удалось найти его ID")
    #возвращаем важную херобору, тронете - убью
    return version_id


# === Запуск ===
def launch_selected_build():
    #Загружаем сессию(авторизацию)
    sess = load_session()
    if not sess:
        return messagebox.showerror("Ошибка", "Сначала войдите")

    #Получаем выбранную сборку
    build_name = builds_combobox.get()
    if not build_name:
        return messagebox.showwarning("Сборка", "Не выбрана сборка")

    #Находим метаданные сборки
    build = next((b for b in load_builds() if b["name"] == build_name), None)
    if not build:
        return messagebox.showerror("Сборка", "Метаданные не найдены")

    #Убеждаемся в установке версии, если нету - качаем
    try:
        version_id = ensure_installed(build)
        if not version_id:
            return messagebox.showerror("Ошибка", "Не удалось установить версию Minecraft для сборки")
    except Exception as e:
        return messagebox.showerror("Установка", str(e))

    #Путь к сборке
    build_path = os.path.join(BUILDS_DIR, build_name)
    #Получаем путь к жаве через функцию в начале
    java_path =  get_java_path(build_path)

    max_ram = max_ram_entry.get().strip()
    min_ram = min_ram_entry.get().strip()

    #Параметры запуска
    options = {
        "username": sess["username"],                                        #ник
        "uuid": sess["uuid"],                                                #uuid
        "token": sess["accessToken"],                                        #токен
        "jvmArguments": [                                                    #аргументы жавы
        f"-Xmx{max_ram}",
        f"-Xms{min_ram}",
            f"-javaagent:{AUTHLIB_INJECTOR_PATH}=https://authserver.ely.by"  #говорим жаве заходить через ely.by
        ] + jvm_extra_entry.get().split(),
        "launcherName": "EchoLauncher",                                      #говорим название нашего лаунчера
        "launcherVersion": "1.1",                                            #версию
        "gameDirectory": build_path,               #директорию игры(сборки)
        "executablePath": java_path,                                         #путь к жаве
    }
    #Если нету жавы, жалуемся
    if not os.path.isfile(java_path):
        return messagebox.showerror("Java", f"Не найден файл Java: {java_path}")

    cmd = minecraft_launcher_lib.command.get_minecraft_command(
        version_id,
        build_path,
        options
    )
    subprocess.Popen(cmd)
    messagebox.showinfo("Запуск", f"Сборка {build_name} успешно запущена!")

# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
# === GUI =================================================================================================================================================================================
root = tk.Tk()
root.title("EchoLauncher (builds)")
icon = ImageTk.PhotoImage(file = "photos/launcher.ico")
root.iconphoto(True, icon)
root.geometry("925x530")
root.resizable(False, False)

# фон (опционально)
if os.path.isfile("photos/backgroundphoto.jpg"):
    bg_img = ImageTk.PhotoImage(Image.open("photos/backgroundphoto.jpg"))
    tk.Label(root, image=bg_img).place(relwidth=1, relheight=1)

# Авторизация -------------------------------------------------------------
auth_f = tk.Frame(root, bd=2, relief="solid", padx=10, pady=10)
auth_f.place(x=10, y=10, width=270, height=300)

for lbl in ["Авторизация", "Email или логин:"]:
    tk.Label(auth_f, text=lbl, font=("Arial", 12)).pack(anchor="w")
username_entry = tk.Entry(auth_f, width=30); username_entry.pack(anchor="w")

tk.Label(auth_f, text="Пароль:").pack(anchor="w", pady=(10,0))
password_entry = tk.Entry(auth_f, show="*", width=30); password_entry.pack(anchor="w")

tk.Button(auth_f, text="Войти", width=25, command=login).pack(pady=10)
tk.Button(auth_f, text="Уже входил", command=refresh_session).pack()

# Java настройки ----------------------------------------------------------
java_f = tk.Frame(root, bd=2, relief="solid", padx=10, pady=10)
java_f.place(x=650, y=10, width=260, height=300)

for lbl in ["Настройки Java", "Максимум ОЗУ (например 4G):"]:
    tk.Label(java_f, text=lbl, font=("Arial", 12)).pack(anchor="w")

max_ram_entry = tk.Entry(java_f, width=20)
max_ram_entry.insert(0, java_config.get("max_memory", "4G"))
max_ram_entry.pack(anchor="w")

tk.Label(java_f, text="Минимум ОЗУ (например 2G):").pack(anchor="w", pady=(10, 0))

min_ram_entry = tk.Entry(java_f, width=20)
min_ram_entry.insert(0, java_config.get("min_memory", "2G"))
min_ram_entry.pack(anchor="w")


tk.Label(java_f, text="Доп. аргументы JVM:").pack(anchor="w", pady=(10,0))
jvm_extra_entry = tk.Entry(java_f, width=30)
jvm_extra_entry.insert(0, java_config.get("args", ""))
jvm_extra_entry.pack(anchor="w")

def save_java_settings():
    java_config["max_memory"] = max_ram_entry.get().strip()
    java_config["min_memory"] = min_ram_entry.get().strip()
    java_config["args"] = jvm_extra_entry.get().strip()
    save_java_config(java_config)
    messagebox.showinfo("Сохранено", "Параметры Java сохранены")

tk.Button(java_f, text="Сохранить", command=save_java_settings).pack(pady=10)

# Нижняя панель -----------------------------------------------------------
bottom = tk.Frame(root, bd=2, relief="solid", padx=10, pady=10)
bottom.place(x=10, y=450, width=900, height=70)

bottom.grid_columnconfigure(4, weight=1)

# Combobox сборок
builds_label = tk.Label(bottom, text="Сборка:")
builds_label.grid(row=0, column=0, padx=5)

builds_combobox = ttk.Combobox(bottom, state="readonly", width=25)

def refresh_builds_cb():
    names = [b["name"] for b in load_builds()]
    builds_combobox["values"] = names
    if names:
        builds_combobox.set(names[0])
refresh_builds_cb()

builds_combobox.grid(row=0, column=1, padx=5)

# Кнопки создания/удаления/открытия --------------------------------------

def create_build_dialog():
    dlg = tk.Toplevel(root)
    dlg.title("Новая сборка")
    tk.Label(dlg, text="Название:").grid(row=0,column=0,padx=5,pady=5)
    name_e = tk.Entry(dlg,width=25); name_e.grid(row=0,column=1,padx=5)

    tk.Label(dlg, text="Тип:").grid(row=1,column=0,padx=5)
    type_cb = ttk.Combobox(dlg, state="readonly", values=["vanilla","fabric","forge"], width=22)
    type_cb.set("vanilla")
    type_cb.grid(row=1,column=1,padx=5)

    tk.Label(dlg, text="Версия:").grid(row=2,column=0,padx=5)
    ver_cb = ttk.Combobox(dlg, state="readonly", width=22)
    ver_cb.grid(row=2,column=1,padx=5)

    def update_versions(*_):
        vt = type_cb.get()
        if vt == "vanilla":
            ver_cb["values"] = vanilla_version_ids
        elif vt == "fabric":
            ver_cb["values"] = fabric_version_ids
        else:  # forge
            fetch_forge_promos()
            ver_cb["values"] = sorted(forge_promos_cache.keys())
        if ver_cb["values"]:
            ver_cb.set(ver_cb["values"][0])
    type_cb.bind("<<ComboboxSelected>>", update_versions)
    update_versions()

    def ok():
        try:
            add_build(name_e.get().strip(), ver_cb.get(), type_cb.get())
        except Exception as e:
            messagebox.showerror("Ошибка", str(e)); return
        refresh_builds_cb(); dlg.destroy()
    tk.Button(dlg, text="Создать", command=ok).grid(row=3,column=0,columnspan=2,pady=10)


def delete_build():
    n = builds_combobox.get()
    if not n:
        return
    if not messagebox.askyesno("Удалить", f"Удалить сборку {n}? Папка тоже будет удалена."):
        return
    builds = load_builds(); builds = [b for b in builds if b["name"] != n]; save_builds(builds)
    # удаляем папку
    import shutil; shutil.rmtree(os.path.join(BUILDS_DIR, n), ignore_errors=True)
    refresh_builds_cb()


def open_build_folder():
    n = builds_combobox.get()
    if not n:
        return
    path = os.path.join(BUILDS_DIR, n)
    if os.path.isdir(path):
        webbrowser.open(path)

# Кнопки ------------------------------------------------------------------
# иконки
icon_new   = ImageTk.PhotoImage(Image.open("photos/sozdaty.png").resize((45, 45)))
icon_del   = ImageTk.PhotoImage(Image.open("photos/udality.png").resize((45, 45)))
icon_open  = ImageTk.PhotoImage(Image.open("photos/papka.png").resize((45, 45)))
# сами кнопки
btn_new  = tk.Button(bottom, image=icon_new,  command=create_build_dialog, width=50, height=50)
btn_del  = tk.Button(bottom, image=icon_del,  command=delete_build,        width=50, height=50)
btn_open = tk.Button(bottom, image=icon_open, command=open_build_folder,   width=50, height=50)
for i,b in enumerate([btn_new, btn_del, btn_open], start=2):
    b.grid(row=0, column=i, padx=2)

launch_btn = tk.Button(bottom, text="🚀 Запустить Minecraft", width=30, command=launch_selected_build, bg="#4CAF50", fg="white", font=("Arial",14,"bold"))
launch_btn.grid(row=0, column=5, padx=20)

bottom.grid_columnconfigure(5, weight=1)

root.mainloop()
