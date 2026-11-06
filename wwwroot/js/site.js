(() => {
  function setButtonLoading(button, isLoading) {
    const label = button.querySelector("[data-button-label]");
    const spinner = button.querySelector("[data-button-spinner]");
    if (!label || !spinner) {
      throw new Error("Loading buttons need a label and spinner element.");
    }

    if (isLoading) {
      if (!button.dataset.idleLabel) {
        button.dataset.idleLabel = label.textContent;
      }
      label.textContent = button.dataset.loadingText || "Sedang memproses…";
      spinner.hidden = false;
      button.disabled = true;
      button.setAttribute("aria-busy", "true");
      return;
    }

    label.textContent = button.dataset.idleLabel || label.textContent;
    spinner.hidden = true;
    button.disabled = false;
    button.setAttribute("aria-busy", "false");
  }

  window.AppUi = { setButtonLoading };

  document.addEventListener("submit", async (event) => {
    const form = event.target;
    if (
      !(form instanceof HTMLFormElement) ||
      !form.matches("[data-ajax-form]")
    ) {
      return;
    }

    event.preventDefault();
    const button =
      event.submitter ||
      form.querySelector('button[type="submit"], button:not([type])');
    if (!button) {
      return;
    }

    if (button.disabled) {
      return;
    }

    let feedback = form.querySelector(".form-action-feedback");
    if (!feedback) {
      feedback = document.createElement("p");
      feedback.className = "form-action-feedback";
      feedback.setAttribute("role", "status");
      feedback.setAttribute("aria-live", "polite");
      form.append(feedback);
    }
    feedback.hidden = true;
    feedback.textContent = "";
    setButtonLoading(button, true);

    let navigating = false;
    try {
      const response = await fetch(form.action, {
        method: form.method,
        body: new FormData(form),
        credentials: "same-origin",
      });

      if (response.redirected) {
        navigating = true;
        window.location.assign(response.url);
        return;
      }

      const contentType = response.headers.get("content-type") || "";
      if (contentType.includes("text/html")) {
        const documentFromResponse = new DOMParser().parseFromString(
          await response.text(),
          "text/html",
        );
        const nextCard = documentFromResponse.querySelector(".join-card");
        const currentCard = form.closest(".join-card");

        if (response.ok && nextCard && currentCard) {
          currentCard.replaceWith(nextCard);
          document.title = documentFromResponse.title;
          if (
            window.jQuery &&
            window.jQuery.validator &&
            window.jQuery.validator.unobtrusive
          ) {
            window.jQuery.validator.unobtrusive.parse(nextCard);
          }
          nextCard
            .querySelector(
              "input.input-validation-error, input.is-invalid, input[aria-invalid='true']",
            )
            ?.focus();
          return;
        }

        const message =
          documentFromResponse.querySelector(".validation-summary")
            ?.textContent.trim() ||
          documentFromResponse.querySelector('[role="alert"]')
            ?.textContent.trim();
        throw new Error(
          message || "Permintaan tidak berjaya. Sila cuba lagi.",
        );
      }

      if (!response.ok) {
        let message = "Permintaan tidak berjaya. Sila cuba lagi.";
        if (contentType.includes("application/json")) {
          const result = await response.json();
          message = result.message || message;
        }
        throw new Error(message);
      }
    } catch (error) {
      if (feedback) {
        feedback.textContent =
          error instanceof Error
            ? error.message
            : "Permintaan tidak berjaya. Sila cuba lagi.";
        feedback.hidden = false;
      }
    } finally {
      if (!navigating && button.isConnected) {
        setButtonLoading(button, false);
      }
    }
  });
})();
