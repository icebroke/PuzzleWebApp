(() => {
  const root = document.querySelector("#crossword-game");
  if (!root) {
    return;
  }

  const inputs = Array.from(root.querySelectorAll(".crossword-cell"));
  const clueButtons = Array.from(root.querySelectorAll(".clue-button"));
  const directionToggle = root.querySelector("#direction-toggle");
  const directionLabel = root.querySelector("#direction-label");
  const saveStatus = root.querySelector("#save-status");
  const progressLabel = root.querySelector("#progress-label");
  const submitButton = root.querySelector("#submit-puzzle");
  const clearButton = root.querySelector("#clear-answers");
  const submissionBanner = root.querySelector("#submission-banner");
  const submissionFeedback = root.querySelector("#submission-feedback");
  const token = root.querySelector(
    'input[name="__RequestVerificationToken"]',
  ).value;
  const cluesByKey = new Map(
    clueButtons.map((button) => [button.dataset.clueKey, button]),
  );
  let activeDirection = "Across";
  let activeKey = "";
  let saveTimer;
  let saveQueue = Promise.resolve();

  const keyFor = (input, direction) =>
    input.dataset[`${direction.toLowerCase()}Key`];
  const directionName = (direction) =>
    direction === "Across" ? "Melintang" : "Menegak";

  function selectClue(key) {
    const clue = cluesByKey.get(key);
    if (!clue) {
      return;
    }

    activeKey = key;
    activeDirection = clue.dataset.direction;
    directionLabel.textContent = directionName(activeDirection);
    directionToggle.setAttribute(
      "aria-label",
      `Tukar arah petunjuk, kini ${directionName(activeDirection)}`,
    );
    clueButtons.forEach((button) => {
      const selected = button === clue;
      button.classList.toggle("is-active", selected);
      button.setAttribute("aria-pressed", String(selected));
    });
    inputs.forEach((input) => {
      const selected = keyFor(input, activeDirection) === activeKey;
      input.classList.toggle("in-active-word", selected);
      input.classList.toggle(
        "is-selected",
        selected && document.activeElement === input,
      );
    });
  }

  function chooseClueForCell(input, preferToggle = false) {
    const acrossKey = keyFor(input, "Across");
    const downKey = keyFor(input, "Down");
    let nextKey;

    if (acrossKey && downKey) {
      if (preferToggle && activeKey === acrossKey) {
        nextKey = downKey;
      } else if (preferToggle && activeKey === downKey) {
        nextKey = acrossKey;
      } else if (activeKey === acrossKey || activeKey === downKey) {
        nextKey = activeKey;
      } else {
        nextKey = activeDirection === "Down" ? downKey : acrossKey;
      }
    } else {
      nextKey = acrossKey || downKey;
    }

    if (nextKey) {
      selectClue(nextKey);
    }
  }

  function moveWithinClue(input, offset) {
    const clue = cluesByKey.get(activeKey);
    if (!clue) {
      return;
    }

    const startRow = Number(clue.dataset.startRow);
    const startColumn = Number(clue.dataset.startColumn);
    const currentRow = Number(input.dataset.row);
    const currentColumn = Number(input.dataset.column);
    const index =
      activeDirection === "Across"
        ? currentColumn - startColumn
        : currentRow - startRow;
    const targetIndex = index + offset;
    const length = Number(clue.querySelector(".clue-length").textContent);
    if (targetIndex < 0 || targetIndex >= length) {
      return;
    }

    const row = startRow + (activeDirection === "Down" ? targetIndex : 0);
    const column =
      startColumn + (activeDirection === "Across" ? targetIndex : 0);
    const target = root.querySelector(
      `.crossword-cell[data-row="${row}"][data-column="${column}"]`,
    );
    target?.focus();
  }

  function moveByArrow(input, key) {
    const offsets = {
      ArrowLeft: [0, -1],
      ArrowRight: [0, 1],
      ArrowUp: [-1, 0],
      ArrowDown: [1, 0],
    };
    const [rowOffset, columnOffset] = offsets[key];
    const row = Number(input.dataset.row) + rowOffset;
    const column = Number(input.dataset.column) + columnOffset;
    const target = root.querySelector(
      `.crossword-cell[data-row="${row}"][data-column="${column}"]`,
    );
    target?.focus();
  }

  function getLetters() {
    const letters = {};
    inputs.forEach((input) => {
      const letter = input.value.trim().toUpperCase();
      if (letter) {
        letters[`${input.dataset.row},${input.dataset.column}`] = letter;
      }
    });
    return letters;
  }

  function updateAnsweredCount(answeredCount, totalClues) {
    progressLabel.textContent = `${answeredCount} daripada ${totalClues} petunjuk dijawab`;
  }

  async function sendProgress(snapshot) {
    saveStatus.textContent = "Menyimpan kemajuan…";
    const response = await fetch("/Home/SaveProgress", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: token,
      },
      body: JSON.stringify({ letters: snapshot }),
    });
    let result;
    try {
      result = await response.json();
    } catch {
      throw new Error(
        "Pelayan memulangkan respons simpanan yang tidak dapat dibaca.",
      );
    }

    if (response.status === 429) {
      window.location.assign(result.redirectUrl || "/Home/RateLimitError");
      throw new Error(
        result.message ||
          "Terlalu banyak permintaan dibuat. Sila tunggu sebentar.",
      );
    }

    if (!response.ok) {
      throw new Error(result.message || "Data tidak dapat disimpan.");
    }

    updateAnsweredCount(result.answeredCount, result.totalClues);
    saveStatus.textContent = "Kemajuan disimpan";
  }

  function saveProgress(snapshot = getLetters()) {
    const saveOperation = saveQueue.then(() => sendProgress(snapshot));
    saveQueue = saveOperation.catch((error) => {
      saveStatus.textContent =
        error.message || "Kemajuan tidak dapat disimpan. Sila cuba lagi.";
      saveStatus.closest(".game-status").classList.add("has-error");
    });
    saveStatus.closest(".game-status").classList.remove("has-error");
    return saveOperation;
  }

  function scheduleSave() {
    window.clearTimeout(saveTimer);
    saveTimer = window.setTimeout(() => saveProgress().catch(() => {}), 350);
    saveStatus.textContent = "Perubahan belum disimpan";
  }

  inputs.forEach((input) => {
    input.addEventListener("focus", () => {
      chooseClueForCell(input);
      input.classList.add("is-selected");
    });
    input.addEventListener("click", () => {
      chooseClueForCell(input, true);
    });
    input.addEventListener("input", () => {
      input.value = input.value
        .toUpperCase()
        .replace(/[^A-Z]/g, "")
        .slice(0, 1);
      scheduleSave();
      if (input.value) {
        moveWithinClue(input, 1);
      }
    });
    input.addEventListener("keydown", (event) => {
      if (event.key.startsWith("Arrow")) {
        event.preventDefault();
        moveByArrow(input, event.key);
      } else if (event.key === "Backspace" && !input.value) {
        event.preventDefault();
        moveWithinClue(input, -1);
      } else if (event.key === "Enter") {
        event.preventDefault();
        chooseClueForCell(input, true);
      }
    });
    input.addEventListener("blur", () => input.classList.remove("is-selected"));
  });

  clueButtons.forEach((button) => {
    button.addEventListener("click", () => {
      selectClue(button.dataset.clueKey);
      const row = button.dataset.startRow;
      const column = button.dataset.startColumn;
      root
        .querySelector(
          `.crossword-cell[data-row="${row}"][data-column="${column}"]`,
        )
        ?.focus();
    });
  });

  directionToggle.addEventListener("click", () => {
    const input = document.activeElement.closest?.(".crossword-cell");
    if (input) {
      chooseClueForCell(input, true);
      return;
    }

    const nextDirection = activeDirection === "Across" ? "Down" : "Across";
    const activeInput = inputs.find((candidate) =>
      keyFor(candidate, nextDirection),
    );
    if (activeInput) {
      selectClue(keyFor(activeInput, nextDirection));
    }
  });

  clearButton?.addEventListener("click", () => {
    if (!window.confirm("Padam semua huruf dalam teka silang kata ini?")) {
      return;
    }

    inputs.forEach((input) => {
      input.value = "";
    });
    scheduleSave();
  });

  submitButton?.addEventListener("click", async () => {
    if (
      !window.confirm(
        "Hantar jawapan anda? Anda tidak boleh mengubahnya selepas dihantar.",
      )
    ) {
      return;
    }

    window.clearTimeout(saveTimer);
    const snapshot = getLetters();
    inputs.forEach((input) => (input.disabled = true));
    submitButton.disabled = true;
    if (clearButton) {
      clearButton.disabled = true;
    }
    submissionFeedback.textContent = "";
    submissionFeedback.classList.remove("has-error");

    try {
      await saveProgress(snapshot);
      saveStatus.textContent = "Menghantar jawapan…";
      const response = await fetch("/Home/Submit", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token,
        },
        body: JSON.stringify({ letters: snapshot }),
      });
      let result;
      try {
        result = await response.json();
      } catch {
        throw new Error(
          "Pelayan memulangkan respons penghantaran yang tidak dapat dibaca.",
        );
      }

      if (response.status === 429) {
        window.location.assign(result.redirectUrl || "/Home/RateLimitError");
        throw new Error(
          result.message ||
            "Terlalu banyak permintaan dibuat. Sila tunggu sebentar.",
        );
      }

      if (!response.ok) {
        throw new Error(result.message || "Jawapan anda tidak dapat dihantar.");
      }

      saveStatus.textContent = "Jawapan telah dihantar";
      submissionFeedback.textContent = "";
      submissionBanner.innerHTML =
        '<span class="completion-icon" aria-hidden="true">✓</span><div><strong>Jawapan anda telah dihantar.</strong><span>Terima kasih. Jawapan tidak boleh diubah lagi.</span></div>';
      submissionBanner.classList.remove("d-none");
      submitButton.remove();
      clearButton?.remove();
      document.querySelector(".keyboard-hint").textContent =
        "Jawapan anda telah dihantar dan tidak boleh diubah lagi.";
    } catch (error) {
      inputs.forEach((input) => (input.disabled = false));
      submitButton.disabled = false;
      if (clearButton) {
        clearButton.disabled = false;
      }
      submissionFeedback.textContent =
        error.message || "Penghantaran jawapan tidak berjaya.";
      submissionFeedback.classList.add("has-error");
    }
  });

  const firstInput = inputs[0];
  if (firstInput) {
    chooseClueForCell(firstInput);
  }
})();
