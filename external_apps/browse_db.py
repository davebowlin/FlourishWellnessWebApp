# A quick app to display the survey status from the database (USED TO TEST ONLY!)

import pyodbc
import tkinter as tk
from tkinter import ttk, messagebox

# --- Configuration ---
DB_SERVER = "ASISQLDBPROD"
DB_DATABASE = "FlourishWellness"
DB_CONN_STR = (
    f"DRIVER={{ODBC Driver 17 for SQL Server}};"
    f"SERVER={DB_SERVER};DATABASE={DB_DATABASE};Trusted_Connection=yes;"
)

SURVEY_STATUS = {0: "Draft", 1: "Closed", 2: "Active", 3: "Archived"}


def get_connection():
    return pyodbc.connect(DB_CONN_STR)


class BrowserApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("FlourishWellness — DB Browser")
        self.root.geometry("1100x720")
        self._survey_map: dict[str, int] = {}
        self._build_ui()
        self._load_surveys()

    # ------------------------------------------------------------------
    # UI construction
    # ------------------------------------------------------------------
    def _build_ui(self):
        # ── Top bar ──────────────────────────────────────────────────
        top = ttk.Frame(self.root, padding="6 4")
        top.pack(fill=tk.X)

        ttk.Label(top, text="Survey:").pack(side=tk.LEFT)
        self.survey_var = tk.StringVar()
        self.survey_combo = ttk.Combobox(
            top, textvariable=self.survey_var, state="readonly", width=32
        )
        self.survey_combo.pack(side=tk.LEFT, padx=6)
        self.survey_combo.bind("<<ComboboxSelected>>", lambda _: self._load_users())

        ttk.Button(top, text="Refresh", command=self._refresh).pack(side=tk.LEFT, padx=4)

        self.status_label = ttk.Label(top, text="", foreground="#555555")
        self.status_label.pack(side=tk.RIGHT, padx=8)

        # ── Vertical pane: user list (top) + response detail (bottom) ─
        paned = ttk.PanedWindow(self.root, orient=tk.VERTICAL)
        paned.pack(fill=tk.BOTH, expand=True, padx=6, pady=(2, 6))

        # ── Users panel ──────────────────────────────────────────────
        user_frame = ttk.LabelFrame(paned, text="Users", padding=4)
        paned.add(user_frame, weight=3)

        user_cols = ("username", "full_name", "status", "responses")
        self.user_tree = ttk.Treeview(
            user_frame, columns=user_cols, show="headings", selectmode="browse"
        )
        self.user_tree.heading("username",  text="Username (AD)")
        self.user_tree.heading("full_name", text="Full Name")
        self.user_tree.heading("status",    text="Survey Status")
        self.user_tree.heading("responses", text="# Responses")
        self.user_tree.column("username",  width=180, anchor=tk.W)
        self.user_tree.column("full_name", width=230, anchor=tk.W)
        self.user_tree.column("status",    width=120, anchor=tk.CENTER)
        self.user_tree.column("responses", width=100, anchor=tk.CENTER)

        u_vsb = ttk.Scrollbar(user_frame, orient=tk.VERTICAL,   command=self.user_tree.yview)
        u_hsb = ttk.Scrollbar(user_frame, orient=tk.HORIZONTAL, command=self.user_tree.xview)
        self.user_tree.configure(yscrollcommand=u_vsb.set, xscrollcommand=u_hsb.set)
        u_vsb.pack(side=tk.RIGHT,  fill=tk.Y)
        u_hsb.pack(side=tk.BOTTOM, fill=tk.X)
        self.user_tree.pack(fill=tk.BOTH, expand=True)

        self.user_tree.tag_configure("complete",   foreground="#1a7a1a")
        self.user_tree.tag_configure("incomplete", foreground="#b36b00")
        self.user_tree.tag_configure("none",       foreground="#888888")

        self.user_tree.bind("<<TreeviewSelect>>", self._on_user_select)

        # ── Responses detail panel ───────────────────────────────────
        resp_frame = ttk.LabelFrame(paned, text="Responses for selected user", padding=4)
        paned.add(resp_frame, weight=2)

        resp_cols = ("question", "answer")
        self.resp_tree = ttk.Treeview(
            resp_frame, columns=resp_cols, show="headings"
        )
        self.resp_tree.heading("question", text="Question")
        self.resp_tree.heading("answer",   text="Answer")
        self.resp_tree.column("question", width=820, anchor=tk.W)
        self.resp_tree.column("answer",   width=120, anchor=tk.CENTER)

        r_vsb = ttk.Scrollbar(resp_frame, orient=tk.VERTICAL,   command=self.resp_tree.yview)
        r_hsb = ttk.Scrollbar(resp_frame, orient=tk.HORIZONTAL, command=self.resp_tree.xview)
        self.resp_tree.configure(yscrollcommand=r_vsb.set, xscrollcommand=r_hsb.set)
        r_vsb.pack(side=tk.RIGHT,  fill=tk.Y)
        r_hsb.pack(side=tk.BOTTOM, fill=tk.X)
        self.resp_tree.pack(fill=tk.BOTH, expand=True)

    # ------------------------------------------------------------------
    # Data loading
    # ------------------------------------------------------------------
    def _load_surveys(self):
        try:
            with get_connection() as conn:
                cursor = conn.cursor()
                cursor.execute(
                    "SELECT Id, Year, Status FROM SurveyEntities ORDER BY Year DESC"
                )
                rows = cursor.fetchall()
        except Exception as exc:
            messagebox.showerror("DB Error", str(exc))
            return

        self._survey_map = {}
        labels = []
        for row in rows:
            label = f"{row.Year} — {SURVEY_STATUS.get(row.Status, str(row.Status))}"
            self._survey_map[label] = row.Id
            labels.append(label)

        self.survey_combo["values"] = labels
        if labels:
            self.survey_combo.current(0)
            self._load_users()

    def _current_survey_id(self) -> int | None:
        return self._survey_map.get(self.survey_var.get())

    def _load_users(self):
        survey_id = self._current_survey_id()
        if survey_id is None:
            return

        self.user_tree.delete(*self.user_tree.get_children())
        self.resp_tree.delete(*self.resp_tree.get_children())

        try:
            with get_connection() as conn:
                cursor = conn.cursor()
                cursor.execute(
                    """
                    SELECT
                        u.Id,
                        u.Email           AS Username,
                        u.FullName,
                        COALESCE(uss.IsCompleted, -1) AS IsCompleted,
                        COUNT(r.Id)       AS ResponseCount
                    FROM Users u
                    LEFT JOIN UserSurveyStatuses uss
                           ON uss.UserId = u.Id AND uss.SurveyEntityId = ?
                    LEFT JOIN Responses r
                           ON r.UserId = u.Id AND r.SurveyEntityId = ?
                    GROUP BY u.Id, u.Email, u.FullName, uss.IsCompleted
                    ORDER BY u.FullName
                    """,
                    survey_id,
                    survey_id,
                )
                rows = cursor.fetchall()
        except Exception as exc:
            messagebox.showerror("DB Error", str(exc))
            return

        total = len(rows)
        completed = 0
        for row in rows:
            if row.IsCompleted == 1:
                status_text, tag = "Completed", "complete"
                completed += 1
            elif row.IsCompleted == 0:
                status_text, tag = "Incomplete", "incomplete"
            else:
                status_text, tag = "No Response", "none"

            self.user_tree.insert(
                "",
                tk.END,
                iid=str(row.Id),
                values=(row.Username, row.FullName, status_text, row.ResponseCount),
                tags=(tag,),
            )

        self.status_label.config(text=f"{completed} / {total} completed")

    def _on_user_select(self, _event=None):
        selection = self.user_tree.selection()
        if not selection:
            return
        user_id = int(selection[0])
        survey_id = self._current_survey_id()

        self.resp_tree.delete(*self.resp_tree.get_children())

        try:
            with get_connection() as conn:
                cursor = conn.cursor()
                cursor.execute(
                    """
                    SELECT q.Text, r.Answer
                    FROM Responses r
                    JOIN Questions q ON q.Id = r.QuestionId
                    WHERE r.UserId = ? AND r.SurveyEntityId = ?
                    ORDER BY q.Id
                    """,
                    user_id,
                    survey_id,
                )
                rows = cursor.fetchall()
        except Exception as exc:
            messagebox.showerror("DB Error", str(exc))
            return

        for row in rows:
            self.resp_tree.insert("", tk.END, values=(row.Text, row.Answer))

        # Update the detail label with user name
        vals = self.user_tree.item(selection[0], "values")
        username = vals[0] if vals else ""
        full_name = vals[1] if len(vals) > 1 else ""
        self.resp_tree.master.config(
            text=f"Responses for: {full_name}  ({username})  — {len(rows)} answer(s)"
        )

    def _refresh(self):
        self._load_surveys()


if __name__ == "__main__":
    root = tk.Tk()
    app = BrowserApp(root)
    root.mainloop()
